using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

// Cliente do Pong (baseado na Prática 5 - UdpClientTwoClients).
// Roda em cada máquina de jogador: move o paddle local com W/S ou setas,
// envia sua posição ao servidor ("PADDLE:y") e recebe de volta o estado
// completo do jogo ("STATE:bolaX;bolaY;paddle1Y;paddle2Y;score1;score2"),
// usado para posicionar a bola e o paddle do adversário na tela.
public class PongClient : MonoBehaviour
{
    [Header("Rede")]
    public string serverIP = "127.0.0.1"; // IP da máquina que roda o PongServer
    public int serverPort = 5001;

    [Header("Objetos da cena")]
    public GameObject localPaddle;
    public GameObject remotePaddle;
    public GameObject ball;
    public Text scoreText; // opcional: UI > Text (Legacy)

    [Header("Movimento")]
    public float paddleSpeed = 8f;
    public float topBound = 4.5f;
    public float bottomBound = -4.5f;
    public float leftPaddleX = -8f;
    public float rightPaddleX = 8f;
    public float smoothing = 15f;

    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;

    volatile int myId = -1;
    bool sidesSet;

    readonly object stateLock = new object();
    Vector3 remoteBallPos;
    float remotePaddleY;
    int score1, score2;
    bool hasState;

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData) { IsBackground = true };
        receiveThread.Start();

        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);
    }

    void ReceiveData()
    {
        while (true)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try
            {
                data = client.Receive(ref remoteEP);
            }
            catch (SocketException)
            {
                break;
            }

            string msg = Encoding.UTF8.GetString(data);

            if (msg.StartsWith("ASSIGN:"))
            {
                myId = int.Parse(msg.Substring("ASSIGN:".Length), CultureInfo.InvariantCulture);
                Debug.Log("[Cliente] Meu ID = " + myId);
            }
            else if (msg.StartsWith("STATE:"))
            {
                string[] parts = msg.Substring("STATE:".Length).Split(';');
                if (parts.Length == 6)
                {
                    float bx = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    float by = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    float p1y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    float p2y = float.Parse(parts[3], CultureInfo.InvariantCulture);
                    int s1 = int.Parse(parts[4], CultureInfo.InvariantCulture);
                    int s2 = int.Parse(parts[5], CultureInfo.InvariantCulture);

                    lock (stateLock)
                    {
                        remoteBallPos = new Vector3(bx, by, 0f);
                        remotePaddleY = (myId == 1) ? p2y : p1y;
                        score1 = s1;
                        score2 = s2;
                        hasState = true;
                    }
                }
            }
        }
    }

    void Update()
    {
        if (myId == -1) return; // aguardando ASSIGN do servidor

        PositionPaddlesBySide();
        MoveLocalPaddle();
        ApplyRemoteState();
    }

    void PositionPaddlesBySide()
    {
        if (sidesSet) return;

        float localX = (myId == 1) ? leftPaddleX : rightPaddleX;
        float remoteX = (myId == 1) ? rightPaddleX : leftPaddleX;

        Vector3 lp = localPaddle.transform.position;
        localPaddle.transform.position = new Vector3(localX, lp.y, lp.z);

        Vector3 rp = remotePaddle.transform.position;
        remotePaddle.transform.position = new Vector3(remoteX, rp.y, rp.z);

        sidesSet = true;
    }

    void MoveLocalPaddle()
    {
        float v = 0f;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v = 1f;
            else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v = -1f;
        }

        Vector3 pos = localPaddle.transform.position;
        pos.y = Mathf.Clamp(pos.y + v * paddleSpeed * Time.deltaTime, bottomBound, topBound);
        localPaddle.transform.position = pos;

        string msg = "PADDLE:" + pos.y.ToString("F2", CultureInfo.InvariantCulture);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);
    }

    void ApplyRemoteState()
    {
        Vector3 targetBall;
        float targetPaddleY;
        lock (stateLock)
        {
            if (!hasState) return;
            targetBall = remoteBallPos;
            targetPaddleY = remotePaddleY;
        }

        ball.transform.position = Vector3.Lerp(ball.transform.position, targetBall, Time.deltaTime * smoothing);

        Vector3 rp = remotePaddle.transform.position;
        Vector3 targetRemote = new Vector3(rp.x, targetPaddleY, rp.z);
        remotePaddle.transform.position = Vector3.Lerp(rp, targetRemote, Time.deltaTime * smoothing);

        if (scoreText != null)
        {
            scoreText.text = score1 + "  x  " + score2;
        }
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        client?.Close();
    }
}
