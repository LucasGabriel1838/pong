using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpClientPong : MonoBehaviour {

    [Header("Conexão")]
    public string serverIp = "192.168.0.10"; // IP da máquina do Servidor
    public int serverPort = 5001;

    [Header("Objetos do jogo")]
    public GameObject localPaddle;   // raquete que ESTE jogador controla
    public GameObject remotePaddle;  // raquete do outro jogador
    public GameObject ball;

    [Header("UI (TextMeshPro)")]
    public TMP_Text scoreText;      // mostra "0  x  0"
    public GameObject winPanel;     // painel desativado por padrão
    public TMP_Text winText;
    public TMP_Text waitingText;    // opcional: mostra "Aguardando outro jogador..."

    [Header("Ajustes")]
    public float paddleSpeed = 8f;
    public float paddleLimitY = 4f;
    public float ballSpeed = 6f;
    public float maxBallSpeed = 14f;
    public float topLimit = 4.5f;
    public float bottomLimit = -4.5f;
    public float leftLimit = -8.5f;
    public float rightLimit = 8.5f;
    public int scoreToWin = 5;

    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;

    int myId = -1;
    float remotePaddleX;
    Vector3 remotePaddlePos;
    Vector3 remoteBallPos;
    Vector3 ballStartPos;
    Vector2 ballVelocity;

    int scoreLeft = 0;
    int scoreRight = 0;
    bool gameOver = false;
    bool ballInitialized = false;
    bool matchStarted = false; // vira true quando o SERVIDOR avisa que os 2 jogadores conectaram

    void Start() {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
        client.Send(Encoding.UTF8.GetBytes("HELLO"), 5);

        remotePaddleX = remotePaddle.transform.position.x;
        remotePaddlePos = remotePaddle.transform.position;
        ballStartPos = ball.transform.position;
        remoteBallPos = ballStartPos;

        if (winPanel != null) winPanel.SetActive(false);
        if (waitingText != null) waitingText.text = "Aguardando outro jogador...";
        UpdateScoreUI();
    }

    void Update() {
        if (gameOver || myId == -1) return;

        UpdateScoreUI(); // atualiza todo frame: garante que o placar aparece nas 2 máquinas

        if (matchStarted && waitingText != null && waitingText.gameObject.activeSelf) {
            waitingText.gameObject.SetActive(false);
        }

        // ---- Raquete local ----
        float v = Input.GetAxis("Vertical");
        Vector3 p = localPaddle.transform.position;
        p.y = Mathf.Clamp(p.y + v * paddleSpeed * Time.deltaTime, -paddleLimitY, paddleLimitY);
        localPaddle.transform.position = p;

        string paddleMsg = "PADDLE:" + p.y.ToString("F2", CultureInfo.InvariantCulture);
        client.Send(Encoding.UTF8.GetBytes(paddleMsg), paddleMsg.Length);

        // ---- Raquete remota (suaviza até a última posição recebida) ----
        remotePaddle.transform.position = Vector3.Lerp(remotePaddle.transform.position, remotePaddlePos, Time.deltaTime * 10f);

        // ---- Bola (só começa a se mexer depois que os 2 jogadores conectaram) ----
        if (myId == 1) {
            if (matchStarted) {
                if (!ballInitialized) {
                    ResetBall(Random.value > 0.5f ? 1 : -1); // dá a primeira "chacoalhada" na bola
                    ballInitialized = true;
                }
                SimulateBall();
                string ballMsg = "BALL:" +
                    ball.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                    ball.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
                client.Send(Encoding.UTF8.GetBytes(ballMsg), ballMsg.Length);
            }
        } else {
            ball.transform.position = Vector3.Lerp(ball.transform.position, remoteBallPos, Time.deltaTime * 15f);
        }

        CheckWin();
    }

    // Só roda na máquina do Jogador 1 (dono da física da bola).
    // Convenção: Jogador 1 = raquete esquerda (localPaddle), Jogador 2 = raquete direita (remotePaddle, do ponto de vista do jogador 1).
    void SimulateBall() {
        Vector3 pos = ball.transform.position + (Vector3)(ballVelocity * Time.deltaTime);

        if (pos.y >= topLimit) { pos.y = topLimit; ballVelocity.y = -Mathf.Abs(ballVelocity.y); }
        else if (pos.y <= bottomLimit) { pos.y = bottomLimit; ballVelocity.y = Mathf.Abs(ballVelocity.y); }

        ball.transform.position = pos;

        CheckPaddleCollision(localPaddle.transform, isLeft: true);
        CheckPaddleCollision(remotePaddle.transform, isLeft: false);

        if (ball.transform.position.x < leftLimit) {
            scoreRight++;
            OnPointScored(loserSide: -1);
        } else if (ball.transform.position.x > rightLimit) {
            scoreLeft++;
            OnPointScored(loserSide: 1);
        }
    }

    void CheckPaddleCollision(Transform paddle, bool isLeft) {
        float halfHeight = 1.2f; // ajuste conforme o tamanho da sua raquete
        float halfWidth = 0.25f;

        bool withinX = isLeft
            ? Mathf.Abs(ball.transform.position.x - paddle.position.x) <= halfWidth + 0.15f && ballVelocity.x < 0
            : Mathf.Abs(ball.transform.position.x - paddle.position.x) <= halfWidth + 0.15f && ballVelocity.x > 0;

        bool withinY = Mathf.Abs(ball.transform.position.y - paddle.position.y) <= halfHeight;

        if (withinX && withinY) {
            ballVelocity.x = -ballVelocity.x;
            float offset = (ball.transform.position.y - paddle.position.y) / halfHeight;
            ballVelocity.y += offset * 2f;
            float newSpeed = Mathf.Min(ballVelocity.magnitude + 0.4f, maxBallSpeed);
            ballVelocity = ballVelocity.normalized * newSpeed;
        }
    }

    void OnPointScored(int loserSide) {
        string scoreMsg = "SCORE:" + scoreLeft + ";" + scoreRight;
        client.Send(Encoding.UTF8.GetBytes(scoreMsg), scoreMsg.Length);
        UpdateScoreUI();

        if (scoreLeft < scoreToWin && scoreRight < scoreToWin) {
            ResetBall(loserSide);
        }
    }

    void ResetBall(int loserSide) {
        ball.transform.position = ballStartPos;
        float dirX = loserSide;
        float dirY = Random.Range(-0.5f, 0.5f);
        ballVelocity = new Vector2(dirX, dirY).normalized * ballSpeed;
    }

    void CheckWin() {
        if (gameOver) return;
        if (scoreLeft >= scoreToWin || scoreRight >= scoreToWin) {
            gameOver = true;
            string vencedor = scoreLeft >= scoreToWin ? "Jogador 1 (esquerda)" : "Jogador 2 (direita)";
            if (winPanel != null) winPanel.SetActive(true);
            if (winText != null) winText.text = vencedor + " venceu!";
            Debug.Log(vencedor + " venceu! Placar final: " + scoreLeft + " x " + scoreRight);
        }
    }

    void UpdateScoreUI() {
        if (scoreText != null) scoreText.text = scoreLeft + "   x   " + scoreRight;
    }

    void ReceiveData() {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true) {
            byte[] data = client.Receive(ref remoteEP);
            string msg = Encoding.UTF8.GetString(data);

            if (msg.StartsWith("ASSIGN:")) {
                myId = int.Parse(msg.Substring(7));
                Debug.Log("[Cliente] Meu ID = " + myId);
            }
            else if (msg.StartsWith("START:")) {
                matchStarted = true;
                Debug.Log("[Cliente] Os 2 jogadores conectaram, partida começou!");
            }
            else if (msg.StartsWith("PADDLE:")) {
                string[] parts = msg.Substring(7).Split(';');
                if (parts.Length == 2) {
                    int id = int.Parse(parts[0]);
                    if (id != myId) {
                        float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        remotePaddlePos = new Vector3(remotePaddleX, y, 0);
                    }
                }
            }
            else if (msg.StartsWith("BALL:")) {
                if (myId != 1) { // quem não é dono da bola só recebe
                    string[] parts = msg.Substring(5).Split(';');
                    if (parts.Length == 2) {
                        float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        remoteBallPos = new Vector3(x, y, 0);
                    }
                }
            }
            else if (msg.StartsWith("SCORE:")) {
                string[] parts = msg.Substring(6).Split(';');
                if (parts.Length == 2) {
                    scoreLeft = int.Parse(parts[0]);
                    scoreRight = int.Parse(parts[1]);
                }
            }
        }
    }

    void OnApplicationQuit() {
        receiveThread.Abort();
        client.Close();
    }
}