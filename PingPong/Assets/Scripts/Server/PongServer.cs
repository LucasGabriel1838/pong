using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServerPong : MonoBehaviour {

    UdpClient server;
    IPEndPoint anyEP;
    Thread receiveThread;
    Dictionary<string, int> clientIds = new Dictionary<string, int>();
    Dictionary<int, IPEndPoint> endpointsById = new Dictionary<int, IPEndPoint>();
    int nextId = 1;
    bool matchStarted = false;

    void Start() {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
        Debug.Log("Servidor iniciado na porta 5001");
    }

    void ReceiveData() {
        while (true) {
            byte[] data = server.Receive(ref anyEP);
            string msg = Encoding.UTF8.GetString(data);
            string key = anyEP.Address + ":" + anyEP.Port;

            if (!clientIds.ContainsKey(key)) {
                if (nextId > 2) continue; // só aceita 2 jogadores
                clientIds[key] = nextId;
                endpointsById[nextId] = new IPEndPoint(anyEP.Address, anyEP.Port);
                string assignMsg = "ASSIGN:" + clientIds[key];
                server.Send(Encoding.UTF8.GetBytes(assignMsg), assignMsg.Length, anyEP);
                nextId++;

                // Assim que o 2º jogador se conecta, avisa os dois que a partida pode começar
                if (endpointsById.Count == 2 && !matchStarted) {
                    matchStarted = true;
                    string startMsg = "START:1";
                    byte[] sdata = Encoding.UTF8.GetBytes(startMsg);
                    foreach (var kvp in endpointsById)
                        server.Send(sdata, sdata.Length, kvp.Value);
                }
            }
            int id = clientIds[key];

            // Raquete: cada cliente manda só o Y; servidor insere o id e reenvia pra todos
            if (msg.StartsWith("PADDLE:")) {
                string coords = msg.Substring(7);
                string broadcast = $"PADDLE:{id};{coords}";
                byte[] bdata = Encoding.UTF8.GetBytes(broadcast);
                foreach (var kvp in endpointsById)
                    server.Send(bdata, bdata.Length, kvp.Value);
            }
            // Bola: só o jogador 1 (dono da física) manda; servidor reenvia pro jogador 2
            else if (msg.StartsWith("BALL:")) {
                if (id == 1) {
                    byte[] bdata = Encoding.UTF8.GetBytes(msg);
                    foreach (var kvp in endpointsById)
                        if (kvp.Key != 1) server.Send(bdata, bdata.Length, kvp.Value);
                }
            }
            // Placar: reenvia pra todos
            else if (msg.StartsWith("SCORE:")) {
                byte[] bdata = Encoding.UTF8.GetBytes(msg);
                foreach (var kvp in endpointsById)
                    server.Send(bdata, bdata.Length, kvp.Value);
            }
        }
    }

    void OnApplicationQuit() {
        receiveThread.Abort();
        server.Close();
    }
}