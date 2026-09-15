# Pong em rede (3 máquinas) — como montar a cena

Scripts: [`Server/PongServer.cs`](Server/PongServer.cs) e [`Client/PongClient.cs`](Client/PongClient.cs).
Mesma lógica da Prática 5 (ASSIGN de ID por endpoint, protocolo em texto via UDP),
adaptada para Pong: o servidor é dono da bola e do placar; cada cliente só controla
seu próprio paddle.

## 1 máquina — Servidor

1. Cena separada (ex.: `ServerScene`), sem precisar de paddles nem bola visuais.
2. Crie um GameObject vazio `Server` e adicione o componente `PongServer`.
3. Rode/faça build dessa cena nessa máquina. Ela escuta UDP na porta `5001`
   (campo `port`, ajustável) e faz o build do jogo (simulação da bola e placar).
4. Libere a porta UDP `5001` no firewall dessa máquina para conexões de entrada.

## 2 máquinas — Cliente (uma por jogador)

1. Cena do jogo (ex.: `ClientScene`) com:
   - `LocalPaddle` e `RemotePaddle` — dois sprites (retângulos).
   - `Ball` — um sprite (círculo).
   - Opcional: `Canvas > Text (Legacy)` para o placar.
2. Crie um GameObject vazio `Client`, adicione o componente `PongClient` e arraste
   `LocalPaddle`, `RemotePaddle`, `Ball` (e o `Text` do placar, se usar) nos campos.
3. Em `Server IP`, coloque o IP da máquina do servidor na rede local
   (ex.: `192.168.0.10`) — **não** `127.0.0.1`, pois são máquinas diferentes.
   `Server Port` deve ser igual ao do servidor (`5001`).
4. Faça o build e instale nas duas máquinas de jogador. Cada uma se conecta,
   recebe `ASSIGN:1` ou `ASSIGN:2` do servidor e o script posiciona sozinho o
   paddle local à esquerda (jogador 1) ou à direita (jogador 2).
5. Controles: `W`/`S` ou setas ↑/↓ (Input System novo, via `Keyboard.current`).

## Protocolo (texto, igual ao espírito da Prática 5)

- Cliente → Servidor: `HELLO` (uma vez, ao iniciar) e `PADDLE:<y>` (a cada frame).
- Servidor → Cliente: `ASSIGN:<id>` (uma vez, ao conectar).
- Servidor → Clientes: `STATE:<bolaX>;<bolaY>;<paddle1Y>;<paddle2Y>;<score1>;<score2>`
  (broadcast periódico, ~33x/s).

## Ordem de execução

Inicie sempre o servidor primeiro; os clientes podem entrar em qualquer ordem
(o primeiro a mandar `HELLO` vira jogador 1, o segundo vira jogador 2).
