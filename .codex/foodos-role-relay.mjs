import net from "node:net";
import { spawn } from "node:child_process";

const server = net.createServer((socket) => {
  const relay = spawn("cmd.exe", ["/d", "/s", "/c", "docker.exe exec -i foodos-role-pg socat STDIO TCP:127.0.0.1:5432"], {
    stdio: ["pipe", "pipe", "inherit"],
  });
  socket.pipe(relay.stdin);
  relay.stdout.pipe(socket);
  socket.on("close", () => relay.kill());
  socket.on("error", () => relay.kill());
  relay.on("close", () => socket.destroy());
});

server.listen(55442, "127.0.0.1", () => process.stdout.write("READY\n"));
