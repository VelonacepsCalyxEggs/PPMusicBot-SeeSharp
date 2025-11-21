# PPMusicBot-SeeSharp

A rewrite of a Discord music bot originally written in JavaScript/TypeScript, now implemented in C# using Discord.NET and Lavalink. This repository contains the C# project for a Discord music bot that connects to a Lavalink server to stream audio to voice channels.
Not anything unique really.

## About

This project is a C# Discord music bot that uses Discord.NET for interacting with Discord and Lavalink for audio streaming. It offloads audio decoding/encoding and streaming to Lavalink, allowing the C# bot to control playback and manage queues.

Goal: provide a maintainable, extensible, and efficient music bot architecture in C#.

## Features

- Play music from URLs (YouTube, direct mp3, icecast, etc.) via Lavalink
- All the expected features of a player, queue, skip, etc.
- Can query my music database and play music from it via fromdb command.

## Requirements

- .NET SDK (9.0 or newer recommended)
- A Discord bot application with a Bot token: https://discord.com/developers/applications
- Java 17 or higher (Lavalink requires a Java runtime)
- Lavalink server (jar) - separate process
- FFmpeg binary in PATH

---

## Example configuration files

Sample `appsettings.json`:
You might want to use dotnet secrets for secrets. (duh)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Bot": {
    "Token": "HACKME"
  },
  "Lavalink": {
    "BaseAddress": "http://localhost:2333",
    "WebSocketUri": "ws://localhost:2333",
    "Passphrase": "youshallnotpass",
    "HttpClient": {
      "Timeout": "00:00:30"
    }
  },
  "KenobiAPI": {
    "Enabled": true,
    "ApiKey": "HACKMEHARDER",
    "BaseUrl": "https://www.funckenobi42.space/api/file/createMusicStream",
    "SearchEngine": {
      "LOW_THRESHOLD": 200,
      "HIGH_THERSHHOLD": 800,
      "MAX_SUGGESTIONS": 5
    }
  },
  "Database": {
    "ConnectionString": "Host=myserver;Username=mylogin;Password=mypass;Database=mydatabase"
  }
}
```

## Contributing
Contributions are welcome!
