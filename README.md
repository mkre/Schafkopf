# Schafkopf
This is an Open Source implementation of the bavarian card game **Schafkopf**.

Feel free to check out the demo at: https://schafkopf.p4u1.de

Or run it on your own server with docker: `docker run -p 9080:80 -p 9443:443 thielepaul/schafkopf`

What can this app offer you:
* Play Schafkopf with friends in their browser
* No logins, no registration, no ads
* It's Open Source: feel free to adapt it to your needs
* No data is stored permanently on the server

Note, that this is a German game so everything in the game is in German.

## Features
* Sauspiel
* Farbsolo
* Wenz
* Hochzeit
* Ramsch
* Chat
* More than 4 Players (additional players can spectate if not playing)

## Screenshots

![screenshot of app in light mode](screenshots/light.png "Light Mode")

![screenshot of app in dark mode](screenshots/dark.png "Dark Mode")

## Development
This is a .NET core project, check out https://dotnet.microsoft.com/download for more information about .NET core.
If you want to play this on a single computer during development, append `&session=new` to the URL to create a new session instead of reconnecting to an existing one.

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
