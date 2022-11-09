﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;
using Schafkopf.Logic;

namespace Schafkopf.Models
{
    public class Game
    {
        public GameState GameState = new GameState();

        public async Task Reset(SchafkopfHub hub)
        {
            if (GameState.CurrentGameState == State.Idle)
            {
                return;
            }
            if (GameState.CurrentGameState == State.Playing)
            {
                GameState.NewTrick();
                await GameState.Trick.SendTrick(hub, this, GetPlayingPlayersConnectionIds());
                await SendLastTrickButton(hub, GetPlayingPlayersConnectionIds(), LastTrickButtonState.disabled);
                await SendTakeTrickButton(hub, GetPlayingPlayersConnectionIds());
            }
            await ClearGameInfo(hub, GetPlayingPlayersConnectionIds());

            GameState.Reset();
            await SendPlayersInfo(hub);
            foreach (Player player in GameState.Players)
            {
                await player.SendHand(hub);
            }
            await SendPlayers(hub);
            foreach (String connectionId in GetPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseAnnounceGameTypeModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseGameColorModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseGameOverModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseWantToSpectateModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseAllowSpectatorModal");
            }
            foreach (Player player in GameState.Players)
            {
                if (GameState.Players.Where((p => p.GetConnectionIds().Count > 0)).ToList().Count > 4)
                {
                    await SendAskWantToPlay(hub, player.GetConnectionIds());
                }
                else if (player.GetConnectionIds().Count > 0)
                {
                    await PlayerPlaysTheGame(player, hub);
                }
            }
        }

        public async Task ResetIfAllConnectionsLost(SchafkopfHub hub)
        {
            foreach (Player player in GameState.PlayingPlayers)
            {
                if (player.GetConnectionIds().Count > 0)
                {
                    return;
                }
            }
            await Reset(hub);
        }

        public async Task DealCards(SchafkopfHub hub)
        {
            await SendPlayers(hub);
            await SendAskWantToSpectate(hub, GetNonPlayingPlayersConnectionIds());
            foreach (String connectionId in GetPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }

            GameState.StartGame();
            foreach (Player player in GameState.PlayingPlayers)
            {
                await player.SendHand(hub);
            }

            await SendStartPlayer(hub, GetPlayingPlayersConnectionIds());
            if (await CheckIfOnePlayerHas6Nixerl(hub))
            {
                return;
            }
            await SendAskAnnounceInit(hub);
        }

        private async Task<bool> CheckIfOnePlayerHas6Nixerl(SchafkopfHub hub)
        {
            List<Player> players = GameState.PlayingPlayers.Where(
                                        p => p.GetHandCards().Where(
                                            c => !c.IsTrump(GameType.Ramsch, Color.Herz) && c.getPoints() == 0
                                        ).ToList().Count >= 6
                                    ).ToList();
            if (players.Count > 0)
            {
                foreach (String connectionId in GetPlayingPlayersConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("GameOver", $"{String.Join(", ", players.Select(p => p.Name))} hat 6 Nixerl", "");
                }
                return true;
            }
            return false;
        }

        public void DecideWhoIsPlaying()
        {
            GameState.ActionPlayer = GameState.PlayingPlayers.IndexOf(GameState.Players[GameState.StartPlayer]);
            for (int i = 0; i < 4; i++)
            {
                if (GameState.PlayingPlayers[GameState.ActionPlayer].WantToPlay)
                {
                    if (GameState.AnnouncedGame < GameState.PlayingPlayers[GameState.ActionPlayer].AnnouncedGameType)
                    {
                        //Player announces a higher game to play
                        GameState.AnnouncedGame = GameState.PlayingPlayers[GameState.ActionPlayer].AnnouncedGameType;
                        GameState.Leader = GameState.PlayingPlayers[GameState.ActionPlayer];
                    }
                }
                GameState.IncrementActionPlayer();
            }
            GameState.ActionPlayer = GameState.PlayingPlayers.IndexOf(GameState.Leader);
        }

        public async Task StartGame(SchafkopfHub hub)
        {
            GameState.CurrentGameState = State.Playing;
            await SendPlayerIsPlayingGameTypeAndColor(hub, GetPlayingPlayersConnectionIds());
            GameState.FindTeams();
            GameState.ActionPlayer = GameState.PlayingPlayers.IndexOf(GameState.Players[GameState.StartPlayer]);
            GameState.NewTrick();
            await SendPlayers(hub);
            foreach (Player player in GameState.PlayingPlayers)
            {
                await player.SendHand(hub, GameState.AnnouncedGame, GameState.GetTrumpColor());
            }
        }

        public async Task SendPlayerIsPlayingGameTypeAndColor(SchafkopfHub hub, List<String> connectionIds)
        {
            if (GameState.CurrentGameState != State.Playing)
            {
                return;
            }
            String message = "";
            if (GameState.AnnouncedGame == GameType.Farbsolo)
            {
                message = $"{GameState.Leader.Name} spielt ein {GameState.Leader.AnnouncedColor}-Solo";
            }
            else if (GameState.AnnouncedGame == GameType.Sauspiel)
            {
                message = $"{GameState.Leader.Name} spielt auf die {GameState.Leader.AnnouncedColor}-Sau";
            }
            else if (GameState.AnnouncedGame == GameType.Ramsch)
            {
                message = $"Es wird geramscht!";
            }
            else
            {
                message = $"{GameState.Leader.Name} spielt {GameState.AnnouncedGame}";
            }
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveGameInfo", message);
            }
        }

        public async Task SendStartPlayer(SchafkopfHub hub, List<String> connectionIds)
        {
            if (GameState.CurrentGameState == State.Idle || GameState.CurrentGameState == State.Playing)
            {
                return;
            }
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "ReceiveGameInfo",
                    $"{GameState.Players[GameState.StartPlayer].Name} kommt raus"
                );
            }
        }

        public async Task ClearGameInfo(SchafkopfHub hub, List<String> connectionIds)
        {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveGameInfo", "");
            }
        }

        public async Task PlayCard(Player player, Color cardColor, int cardNumber, SchafkopfHub hub)
        {
            if (GameState.CurrentGameState != State.Playing || player != GameState.PlayingPlayers[GameState.ActionPlayer])
            {
                foreach (String connectionId in player.GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("ReceiveError", "Du bist gerade nicht dran!");
                }
                return;
            }
            if (GameState.Trick.Count == 4)
            {
                await hub.TakeTrick();
            }
            (Card playedCard, string message) = GameState.PlayCard(cardColor, cardNumber, hub, this, player);
            if (playedCard == null)
            {
                foreach (String connectionId in player.GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("ReceiveError", message);
                }
                return;
            }
            await player.SendHand(hub, GameState.AnnouncedGame, GameState.GetTrumpColor());
            GameState.AddCardToTrick(playedCard, player);
            await GameState.Trick.SendTrick(hub, this, GetPlayingPlayersConnectionIds());
            if (GameState.LastTrick != null)
            {
                await SendLastTrickButton(hub, GetPlayingPlayersConnectionIds(), LastTrickButtonState.show);
            }

            if (GameState.Trick.Count < 4)
            {
                GameState.IncrementActionPlayer();
                await SendPlayers(hub);
            }
            else
            {
                GameState.ActionPlayer = GameState.PlayingPlayers.IndexOf(GameState.Trick.Winner);
                await SendPlayers(hub);
                await SendTakeTrickButton(hub, GetPlayingPlayersConnectionIds());
            }
        }

        //-------------------------------------------------
        // The players can choose together to play another game,
        // there will be two options for the main-player
        // new game or quit
        //-------------------------------------------------
        public async Task SendEndGameModal(SchafkopfHub hub, List<String> connectionIds)
        {
            //Show the amount of pointfor each team
            if (GameState.AnnouncedGame > 0)
            {
                (int leaderPoints, int followerPoints) = GameState.GetFinalPoints();
                string gameOverTitle = "";

                //Special case: Bettel, Bettel Ouvert
                if (GameState.AnnouncedGame == GameType.Bettel || GameState.AnnouncedGame == GameType.BettelOuvert)
                {
                    if (leaderPoints == 0)
                    {
                        gameOverTitle = "Der Bettel-Spieler hat gewonnen";
                    }
                    else
                    {
                        gameOverTitle = "Der Bettel-Spieler hat verloren";
                    }
                }
                else
                {
                    if (leaderPoints <= 60)
                    {
                        gameOverTitle = "Die Spieler haben verloren";
                    }
                    else
                    {
                        gameOverTitle = "Die Spieler haben gewonnen";
                    }
                }
                foreach (String connectionId in connectionIds)
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "GameOver",
                        gameOverTitle,
                        $"Spieler: {leaderPoints} Punkte, Andere: {followerPoints} Punkte"
                    );
                }
            }
            else
            {
                List<Player> player = new List<Player>();

                for (int i = 0; i < 4; i++)
                {
                    player.Add(GameState.PlayingPlayers[i]);
                }

                player.OrderBy(o => o.Balance).ToList();
                foreach (String connectionId in connectionIds)
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                    "GameOver",
                    "Ramsch vorbei",
                    String.Join(", ", player.Select(p => $"{p.Name}: {p.Balance} Punkte")));
                }

            }
        }

        //-------------------------------------------------
        // Add a player to the game
        // The amount of player is limitless inside a game
        // The amount of playing players has to be excactly 4
        //-------------------------------------------------
        public async Task AddPlayer(Player player, SchafkopfHub hub)
        {
            await SendPlayersInfo(hub);
            await SendPlayers(hub);
            if (GameState.CurrentGameState == State.Idle)
            {
                await PlayerPlaysTheGame(player, hub);
            }
            else
            {
                await SendAskWantToSpectate(hub, player.GetConnectionIds());
            }
        }

        //-------------------------------------------------
        // Player decides to play the game
        //-------------------------------------------------
        public async Task PlayerPlaysTheGame(Player player, SchafkopfHub hub)
        {
            if (GameState.PlayingPlayers.Count < 4 && GameState.CurrentGameState == State.Idle)
            {
                GameState.SetPlayerPlaying(Playing.Play, player);
                await SendPlayersInfo(hub);
                if (GameState.PlayingPlayers.Count == 4)
                {
                    await DealCards(hub);
                }
            }
            else
            {
                //Sorry, there are too many players who want to play, what about some Netflix and Chill?
            }
        }

        //-------------------------------------------------
        // Player decides to not play the next game
        //-------------------------------------------------
        public async Task PlayerDoesNotPlayTheGame(Player player, SchafkopfHub hub)
        {
            if (GameState.CurrentGameState != State.Idle)
            {
                //Sorry, you can not pause the game during the game. You are able to pause afterwards.
                return;
            }
            GameState.SetPlayerPlaying(Playing.Pause, player);
            await SendPlayersInfo(hub);
            if (GameState.Players.Where((p => p.GetConnectionIds().Count > 0 && p.IsPlaying != Playing.Pause)).ToList().Count <= 4)
            {
                foreach (Player p in GameState.Players.Where((p => p.GetConnectionIds().Count > 0 && p.IsPlaying == Playing.Undecided)))
                {
                    await PlayerPlaysTheGame(p, hub);
                }
            }
        }

        public async Task SendConnectionToPlayerLostModal(SchafkopfHub hub, List<string> connectionIds)
        {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "GameOver",
                    "Verbindung zu Spieler verloren",
                    "Möchtest du neustarten oder auf den anderen Spieler warten?"
                );
            }
        }

        public List<String> GetPlayingPlayersConnectionIds()
        {
            return GameState.PlayingPlayers.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIdsWithSpectators()).ToList());
        }

        public List<String> GetNonPlayingPlayersConnectionIds()
        {
            return GameState.Players
                    .Where(p => !GameState.PlayingPlayers.Contains(p))
                    .Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIdsWithSpectators()).ToList());
        }

        public List<String> GetPlayersConnectionIds()
        {
            return GameState.Players.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIds()).ToList());
        }

        public async Task SendPlayers(SchafkopfHub hub)
        {
            if (GameState.PlayingPlayers.Count != 4)
            {
                foreach (Player player in GameState.Players)
                {
                    foreach (String connectionId in player.GetConnectionIds())
                    {
                        await hub.Clients.Client(connectionId).SendAsync(
                            "ReceivePlayers",
                            new String[4] { player.Name, "", "", "" },
                            new String[4] { "", "", "", "" },
                            -1
                        );
                    }
                }
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                String[] permutedPlayers = new String[4];
                String[] permutedPlayerInfos = new String[4];
                for (int j = 0; j < 4; j++)
                {
                    permutedPlayers[j] = GameState.PlayingPlayers[(j + i) % 4].Name + GameState.PlayingPlayers[(j + i) % 4].GetSpectatorNames();
                    permutedPlayerInfos[j] = GameState.PlayingPlayers[(j + i) % 4].GetCurrentInfo(this);
                }
                foreach (String connectionId in GameState.PlayingPlayers[i].GetConnectionIdsWithSpectators())
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceivePlayers",
                        permutedPlayers,
                        permutedPlayerInfos,
                        GameState.ActionPlayer >= 0 ? (GameState.ActionPlayer + 4 - i) % 4 : GameState.ActionPlayer
                    );
                }
            }
        }

        public async Task SendAskAnnounce(SchafkopfHub hub)
        {
            foreach (String connectionId in GameState.PlayingPlayers[GameState.ActionPlayer].GetConnectionIdsWithSpectators())
            {
                await hub.Clients.Client(connectionId).SendAsync("AskAnnounce");
            }
        }

        public async Task SendAskAnnounceInit(SchafkopfHub hub)
        {
            GameState.AnnouncedGame = GameType.Ramsch;
            GameState.Leader = null;
            GameState.CurrentGameState = State.Announce;
            GameState.ActionPlayer = GameState.PlayingPlayers.IndexOf(GameState.Players[GameState.StartPlayer]);
            await SendPlayers(hub);
            await SendAskAnnounce(hub);
        }

        public async Task SendAskForGameType(SchafkopfHub hub)
        {
            for (int i = 0; i < 4; i++)
            {
                if (GameState.PlayingPlayers[GameState.ActionPlayer].WantToPlay)
                {
                    // game type not anounced
                    if (GameState.PlayingPlayers[GameState.ActionPlayer].AnnouncedGameType == GameType.Ramsch)
                    {
                        foreach (String connectionId in GameState.PlayingPlayers[GameState.ActionPlayer].GetConnectionIdsWithSpectators())
                        {
                            await hub.Clients.Client(connectionId).SendAsync("AskGameType");
                        }
                    }
                    // game type already anounnced for everyone
                    else
                    {
                        GameState.CurrentGameState = State.AnnounceGameColor;
                        // decide who plays and ask for color
                        DecideWhoIsPlaying();
                        await SendPlayers(hub);
                        await SendAskForGameColor(hub);
                    }
                    return;
                }
                GameState.IncrementActionPlayer();
                await SendPlayers(hub);
            }
            // no one wants to play => it's a ramsch
            await StartGame(hub);
        }
        public async Task SendAskForGameColor(SchafkopfHub hub)
        {
            // Leader has to choose a color he wants to play with or a color to escort his solo
            if (GameState.AnnouncedGame == GameType.Sauspiel || GameState.AnnouncedGame == GameType.Farbsolo)
            {
                foreach (String connectionId in GameState.Leader.GetConnectionIdsWithSpectators())
                {
                    await hub.Clients.Client(connectionId).SendAsync("AskColor");
                }
            }
            else
            {
                await StartGame(hub);
            }
        }

        public async Task SendAskWantToSpectate(SchafkopfHub hub, List<String> connectionIds)
        {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("AskWantToSpectate", GameState.PlayingPlayers.Select(p => p.Name));
            }
        }
        public async Task SendAskWantToPlay(SchafkopfHub hub, List<String> connectionIds)
        {
            int startPlayer = (GameState.StartPlayer + 1) % GameState.Players.Count;
            while (GameState.Players[startPlayer].GetConnectionIds().Count == 0)
            {
                startPlayer = (GameState.StartPlayer + 1) % GameState.Players.Count;
            }
            List<Player> players = GameState.Players.Where(p => p.GetConnectionIds().Count > 0).ToList();
            startPlayer = players.IndexOf(players.Single(p => p.Id == GameState.Players[startPlayer].Id));
            string playerNames = String.Join(", ", players.Select(p => p.Name));
            string startPlayerName = players[startPlayer].Name;
            string proposal =
$@"
{players[startPlayer].Name},
{players[(int)Math.Floor(startPlayer + 1m * players.Count / 4m) % players.Count].Name},
{players[(int)Math.Floor(startPlayer + 2m * players.Count / 4m) % players.Count].Name},
{players[(int)Math.Floor(startPlayer + 3m * players.Count / 4m) % players.Count].Name}
";
            foreach (string connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("AskWantToPlay", playerNames, startPlayerName, proposal);
            }
        }

        public async Task SendUpdatedGameState(Player player, SchafkopfHub hub, List<string> connectionIds)
        {
            await SendPlayers(hub);
            if (GameState.CurrentGameState == State.Playing)
            {
                await SendPlayerIsPlayingGameTypeAndColor(hub, connectionIds);
                await GameState.Trick.SendTrick(hub, this, connectionIds);
                if (GameState.LastTrick != null)
                {
                    await SendLastTrickButton(hub, connectionIds, LastTrickButtonState.show);
                }
                await player.SendHand(hub, GameState.AnnouncedGame, GameState.GetTrumpColor());
                await SendTakeTrickButton(hub, connectionIds);
            }
            else
            {
                await player.SendHand(hub);
                await SendStartPlayer(hub, connectionIds);
            }
            // send modals
            if (GameState.CurrentGameState == State.Playing && GameState.TrickCount == GameState.inital_number_of_cards_per_player)
            {
                await SendEndGameModal(hub, connectionIds);
            }
            foreach (Player p in GameState.PlayingPlayers)
            {
                if (p.GetConnectionIds().Count == 0)
                {
                    await SendConnectionToPlayerLostModal(hub, connectionIds);
                    break;
                }
            }
            if (GameState.ActionPlayer >= 0 && GameState.PlayingPlayers[GameState.ActionPlayer] == player)
            {
                if (GameState.CurrentGameState == State.Announce)
                {
                    await SendAskAnnounce(hub);
                }
                else if (GameState.CurrentGameState == State.AnnounceGameType)
                {
                    await SendAskForGameType(hub);
                }
            }
            if (GameState.Leader == player && GameState.CurrentGameState == State.AnnounceGameColor)
            {
                await SendAskForGameColor(hub);
            }
        }

        public async Task SendPlayersInfo(SchafkopfHub hub)
        {
            foreach (String connectionId in GetPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "ReceivePlayersList",
                    GameState.Players.Where(p => p.GetConnectionIds().Count > 0).Select(p => p.Name),
                    GameState.Players.Where(p => p.GetConnectionIds().Count > 0).Select(p => p.IsPlaying == Playing.Play)
                );
            }
        }

        public async Task SendLastTrickButton(SchafkopfHub hub, List<String> connectionIds, LastTrickButtonState state)
        {
            foreach (string connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveLastTrickButton", state.ToString());
            }
        }

        public async Task SendTakeTrickButton(SchafkopfHub hub, List<String> connectionIds)
        {
            foreach (Player player in GameState.PlayingPlayers)
            {
                foreach (string connectionId in player.GetConnectionIdsWithSpectators())
                {
                    if (!connectionIds.Contains(connectionId))
                    {
                        continue;
                    }
                    if (GameState.Trick.Count < 4)
                    {
                        await hub.Clients.Client(connectionId).SendAsync("ReceiveTakeTrickButton", TakeTrickButtonState.hidden.ToString());
                    }
                    else if (player == GameState.Trick.Winner)
                    {
                        await hub.Clients.Client(connectionId).SendAsync("ReceiveTakeTrickButton", TakeTrickButtonState.won.ToString());
                    }
                    else
                    {
                        await hub.Clients.Client(connectionId).SendAsync("ReceiveTakeTrickButton", TakeTrickButtonState.lost.ToString(), GameState.Trick.Winner.Name);
                    }
                }
            }
        }
    }
}
