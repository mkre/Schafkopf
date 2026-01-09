# Schafkopf
This is an Open Source implementation of the bavarian card game **Schafkopf**, forked from https://github.com/thielepaul/Schafkopf and enhanced. It's work in progress.

Build and run it on your own server with docker: `docker build Schafkopf -t schafkopf && docker run -p 80:80 schafkopf`.

````

## Text-to-Speech (TTS) Architecture

The application includes a client-side TTS feature (Web Speech API) for reading chat messages and playback of server-hosted audio files. The diagram below shows the main components and message flow.

```mermaid
flowchart LR
   subgraph Browser
      UI["UI: Chat & Controls"]
      SignalRClient["SignalR client (browser)"]
      TTSModule["TTS module\n(window.TTS)\n(speechSynthesis + audio element)"]
      UI -->|send chat| SignalRClient
      SignalRClient -->|ReceiveChatMessage(user,msg)| UI
      SignalRClient -->|ReceiveChatMessage(user,msg)| TTSModule
      SignalRClient -->|ReceivePlayTtsFile(url)| TTSModule
      UI -->|play-for-all (button)| SignalRClient
   end

   subgraph Server
      SchafkopfHub["SchafkopfHub (SignalR)"]
      GameState["Game state & Players"]
      StaticFiles["/wwwroot/tts/ (static files)"]
      SchafkopfHub -->|broadcast ReceiveChatMessage| SignalRClient
      SchafkopfHub -->|BroadcastTtsFile(url)| SignalRClient
      SchafkopfHub --> GameState
      StaticFiles --> SignalRClient
   end

   classDef server fill:#f8f9fa,stroke:#333,stroke-width:1px;
   class SchafkopfHub,GameState,StaticFiles server;

```

Test page
- Open the TTS test page in your running instance to exercise the feature: [TTS Test Page](/Home/TtsTest)
- Server audio files are served from `/tts/` and listed by the endpoint `/tts/files`.

Testing tip
- You can test multi-client TTS by opening the app in two different browsers (for example Chrome and Safari) and using an incognito/private window in each to simulate two players at the same table.

Run command
- To run the app from the workspace root use the exact command:

```bash
cd /workspaces/Schafkopf/Schafkopf && dotnet run
```

You can also start the app from a script via npm as described below.

Notes
- Chat TTS uses the browser's SpeechSynthesis API on each client (privacy-friendly, no server audio generation).
- "Play for all" sends the selected server audio URL to the server which relays it to all players via `BroadcastTtsFile`.


What can this app offer you:
* Play Schafkopf with friends in their browser
* No logins, no registration, no ads
* It's Open Source: feel free to adapt it to your needs
* No data is stored permanently on the server

Note, that this is a German game so everything in the game is in German.

## Features
* Sauspiel
* Farbsolo
* Bettel (w/o Heart as trump)
* Wenz
* Geier
* Hochzeit
* Ramsch
* Legen/Klopfen after one has received half of the cards
* Contra/Re/Sup/... (Default: according to schafkopfschule.de rules, i.e. before 2nd card of trick 1 is played)
* Chat
* More than 4 Players (additional players can spectate if not playing)

## Screenshots

![screenshot of app in light mode](screenshots/light.png "Light Mode")

![screenshot of app in dark mode](screenshots/dark.png "Dark Mode")

## Development

### Setup Instructions

This is a .NET 6.0 core project. To set up your development environment:

1. **Install .NET SDK**: Check out https://dotnet.microsoft.com/download for more information about .NET

2. **Generate HTTPS Developer Certificate** (required):
   ```bash
   dotnet dev-certs https
   ```
   This creates a self-signed certificate needed to run the application locally. You only need to do this once per development machine.

3. **Restore NuGet Packages**:
   ```bash
   dotnet restore
   ```

4. **Build the Project**:
   ```bash
   dotnet build
   ```

5. **Run the Application**:
   ```bash
   cd Schafkopf
   dotnet run
   ```
   The application will be available at:
   - HTTP: http://localhost:5000
   - HTTPS: https://localhost:5001

   **Note for Dev Container Users**: If you're running this in VS Code Dev Container, the container is configured with port forwarding. VS Code will automatically detect the running application and may show a notification to open it.

6. **Watch Mode** (for development with auto-reload):
   ```bash
   dotnet watch run
   ```

### Development Tips

- If you want to play this on a single computer during development, append `&session=new` to the URL to create a new session instead of reconnecting to an existing one.
- For troubleshooting setup issues, see [AGENTS.md](AGENTS.md) which contains detailed setup instructions and solutions for common problems.

Using VS Code Tasks

- The workspace contains VS Code tasks defined in `.vscode/tasks.json` with labels: `build`, `publish`, and `watch`.
- To run a task: open the Command Palette (`Ctrl+Shift+P`) and choose `Tasks: Run Task`, then select `watch` (recommended) or `build`.
- Use `watch` for development (`dotnet watch run`) to get auto-reload on code changes. For a simple manual start use the exact run command below.
- Avoid using `publish` for normal local testing — `publish` creates deployable artifacts and is slower.

### Troubleshooting Port Conflicts

**macOS Users**: If HTTP on port 5000 returns a 403 Forbidden error, it's likely that Apple's **AirTunes (AirPlay) server** is using port 5000.

**Solution**: Use HTTPS instead:
```
https://localhost:5001
```

Accept the self-signed certificate warning when prompted.

**Alternative**: Disable AirPlay Receiver on your Mac:
1. System Settings → General → AirDrop & Handoff → Turn off AirPlay Receiver
2. Or: System Settings → General → Sharing → Turn off AirPlay Receiver

Then HTTP on port 5000 will be available.

## Server Installation
In case you want to run the application behind a reverse proxy using nginx, you could use the following configuration to handle the required websocket properly:
```
map $http_upgrade $connection_upgrade {
   default upgrade;
   '' close;
}

upstream schafkopf_docker {
    server 0.0.0.0:9080;
}

server {
    listen 80;
    listen [::]:80;
    server_name    schafkopf.example.com;
    root           /var/www/html;
    index          index index.html index.htm index.nginx-debian.html;

    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name    schafkopf.example.com;
    
    # In case you are using letsencrypt for your certificates
    ssl_certificate    /etc/letsencrypt/live/schafkopf.example.com/fullchain.pem;
    ssl_certificate_key    /etc/letsencrypt/live/schafkopf.example.com/privkey.pem;

    root           /var/www/html;
    index          index index.html index.htm index.nginx-debian.html;


    location / {
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
        proxy_set_header   Host $host;
        proxy_pass http://schafkopf_docker;
    }
}
```
