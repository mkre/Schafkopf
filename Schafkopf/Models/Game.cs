using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;
using Schafkopf.GameState;

namespace Schafkopf.Models
{
    public class Game
    {
        public Schafkopf.GameState.GameState GameState;

        public Game(GameRules rules) {
            GameState = new Schafkopf.GameState.GameState(rules);
        }

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
                await hub.Clients.Client(connectionId).SendAsync("CloseKnockModal");
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
            foreach (String connectionId in GetPlayingPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("OfferBettel", GameState.Rules.isBettelEnabled.ToString());
            }

            GameState.StartGame();
            
            if (GameState.Rules.isKlopfenEnabled)
            {
                foreach (Player player in GameState.PlayingPlayers)
                {
                    await player.SendHalfHand(hub);
                }

                GameState.CurrentGameState = State.Knock;

                await SendAskKnock(hub);
            }
            else
            {
                await FinalizeDealCards(hub);
            }
        }

        public async Task FinalizeDealCards(SchafkopfHub hub)
        {
            foreach (Player player in GameState.PlayingPlayers)
            {
                await player.SendHand(hub);
            }

            await SendStartPlayer(hub, GetPlayingPlayersConnectionIds());
            if (await CheckIfOnePlayerHas6Nixerl(hub))
            {
                return;
            }
            await SendAskAnnounceHochzeit(hub);
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
            GameState.AllowedToAnnounceContraPlayers.Clear();
            GameState.AddPlayerToAllowedToAnnounceContraPlayers(GameState.Group0);
            await SendUpdateContraButton(hub, GetPlayingPlayersConnectionIds());
        }

        public async Task SendPlayerIsPlayingGameTypeAndColor(SchafkopfHub hub, List<String> connectionIds)
        {
            if (GameState.CurrentGameState != State.Playing)
            {
                return;
            }
            String message = "";
            switch (GameState.AnnouncedGame)
            {
                case GameType.Ramsch:
                    message = $"Es wird geramscht!";
                    break;
                case GameType.Hochzeit:
                    message = $"{GameState.Leader.Name} (kein Trumpf) und {GameState.HusbandWife.Name} spielen eine Hochzeit!";
                    break;
                case GameType.Sauspiel:
                    message = $"{GameState.Leader.Name} spielt auf die {GameState.Leader.AnnouncedColor}-Sau";
                    break;
                case GameType.Geier:
                    message = $"{GameState.Leader.Name} spielt einen Geier";
                    break;
                case GameType.Wenz:
                    message = $"{GameState.Leader.Name} spielt einen Wenz";
                    break;
                case GameType.Bettel:
                    message = $"{GameState.Leader.Name} spielt einen Bettel";
                    break;
                case GameType.BettelBrett:
                    message = $"{GameState.Leader.Name} spielt einen Bettel Brett";
                    break;
                case GameType.Farbsolo:
                    message = $"{GameState.Leader.Name} spielt ein {GameState.Leader.AnnouncedColor}-Solo";
                    break;
                case GameType.GeierTout:
                    message = $"{GameState.Leader.Name} spielt einen Geier-Tout";
                    break;
                case GameType.WenzTout:
                    message = $"{GameState.Leader.Name} spielt einen Wenz-Tout";
                    break;
                case GameType.FarbsoloTout:
                    message = $"{GameState.Leader.Name} spielt ein {GameState.Leader.AnnouncedColor}-Solo-Tout";
                    break;
                default:
                    message = $"{GameState.Leader.Name} spielt {GameState.AnnouncedGame}";
                    break;
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

        ///<summary>Method <c>SendUpdateContraButton</c> updated the ContraReSup Button (show/hide) at every player</summary>
        public async Task SendUpdateContraButton(SchafkopfHub hub, List<String> connectionIds)
        {
            String newButtonText = ((ContraState)((int)GameState.CurrentContraState + 1)).ToString() + "!";
            foreach (String connectionId in connectionIds)
            {
                if (GetAllowedToAnnounceContraPlayersConnectionIds().Contains(connectionId))
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "ShowContraReSupButton",
                        newButtonText
                    );
                }
                else
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "HideContraReSupButton"
                    );
                }
            }
        }
        
        public async Task ClearGameInfo(SchafkopfHub hub, List<String> connectionIds)
        {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveGameInfo", "\n#### Neues Spiel beginnt ####");
            }
        }

        public async Task PlayCard(Player player, Color cardColor, int cardNumber, SchafkopfHub hub)
        {
            if (GameState.CurrentGameState == State.HochzeitExchangeCards && player == GameState.HusbandWife)
            {
                bool success = GameState.ExchangeCardWithPlayer(player, cardColor, cardNumber, GameState.Leader, hub, this);

                if (success)
                {
                    foreach (String connectionId in GetPlayingPlayersConnectionIds())
                    {
                        await hub.Clients.Client(connectionId).SendAsync(
                            "ReceiveSystemMessage", $"{GameState.HusbandWife.Name} und {GameState.Leader.Name} haben eine Karte getauscht"
                        );
                    }
                    await StartGame(hub);
                }
                else
                {
                    foreach (String connectionId in player.GetConnectionIds())
                    {
                        await hub.Clients.Client(connectionId).SendAsync("ReceiveError", "Du kannst deinem Mitspieler kein Trumpf geben!");
                    }
                }
                return;
            }
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

            //If card #GameState.Rules.contraMustBeSaidBeforeTrickCard is played in trick #0/1, no one is allowed to announce Contra/Re/Sup anymore
            if (GameState.TrickCount == 0 && GameState.Trick.Count == GameState.Rules.contraMustBeSaidBeforeTrickCard) {
                GameState.AllowedToAnnounceContraPlayers.Clear();
                await SendUpdateContraButton(hub, GetPlayingPlayersConnectionIds());
            }

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

                // For BettelBrett, reveal the leader's cards after first trick
                if (GameState.AnnouncedGame == GameType.BettelBrett && GameState.TrickCount == 1)
                {
                    // Send the leader's cards to all players and mark them as revealed
                    GameState.SetBettelBrettCardsRevealed();
                    foreach (String connectionId in GetPlayingPlayersConnectionIds())
                    {
                        await hub.Clients.Client(connectionId).SendAsync(
                            "RevealPlayerCards",
                            GameState.Leader.Name,
                            GameState.Leader.GetHandCards().Select(c => new { Color = c.Color, Number = c.Number }).ToList()
                        );
                    }
                }
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
            if (GameState.AnnouncedGame != GameType.Ramsch)
            {
                (int leaderPoints, int followerPoints, string leaderNames, string followerNames) = GameState.GetFinalPointsAndTeams();
                string gameOverTitle = "";

                //Special case: Bettel variants
                if (GameState.AnnouncedGame == GameType.Bettel || GameState.AnnouncedGame == GameType.BettelBrett)
                {
                    string gameType = GameState.AnnouncedGame == GameType.BettelBrett ? "Bettel Brett" : "Bettel";
                    if (leaderPoints == 0)
                    {
                        gameOverTitle = $"{leaderNames} hat den {gameType} gewonnen";
                    }
                    else
                    {
                        gameOverTitle = $"{leaderNames} hat den {gameType} verloren";
                    }
                }
                else if (GameState.AnnouncedGame == GameType.GeierTout || GameState.AnnouncedGame == GameType.WenzTout || GameState.AnnouncedGame == GameType.FarbsoloTout)
                {
                    gameOverTitle = leaderNames + " hat ";
                    switch (GameState.AnnouncedGame)
                    {
                        case GameType.GeierTout:
                            gameOverTitle += "den Geier-Tout";
                            break;
                        case GameType.WenzTout:
                            gameOverTitle += "den Wenz-Tout";
                            break;
                        case GameType.FarbsoloTout:
                            gameOverTitle += "das Solo-Tout";
                            break;
                        default:
                            gameOverTitle += "das Spiel";
                            break;
                    }

                    if (leaderPoints >= 120)
                    {
                        gameOverTitle += " gewonnen";
                    }
                    else
                    {
                        gameOverTitle += " verloren";
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

                string gameOverMessage = leaderNames + ": " + leaderPoints.ToString() + " Punkte ||| " + followerNames + ": " + followerPoints.ToString() + " Punkte";

                foreach (String connectionId in connectionIds)
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "GameOver",
                        gameOverTitle,
                        gameOverMessage
                    );

                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceiveGameInfo",
                        gameOverMessage
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

                string gameOverMessage = String.Join(", ", player.Select(p => $"{p.Name}: {p.Balance} Punkte"));

                foreach (String connectionId in connectionIds)
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "GameOver",
                        "Ramsch vorbei",
                        gameOverMessage
                    );

                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceiveGameInfo",
                        gameOverMessage
                    );
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

        //-------------------------------------------------
        // Determines the partner for a Marriage (Hochzeit)
        //-------------------------------------------------
        public void IWantToMarryU(Player p)
        {
            GameState.HusbandWife = p;
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

        public List<String> GetAllowedToAnnounceContraPlayersConnectionIds()
        {
            return GameState.AllowedToAnnounceContraPlayers.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIdsWithSpectators()).ToList());
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
                    if (GameState.PlayingPlayers[(j + i) % 4].TricksWon > 0)
                    {
                        permutedPlayers[j] += (" | " + GameState.PlayingPlayers[(j + i) % 4].TricksWon + " Stich");
                        if (GameState.PlayingPlayers[(j + i) % 4].TricksWon != 1)
                        {
                            permutedPlayers[j] += "e";
                        }
                    }
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

        public async Task SendAskKnock(SchafkopfHub hub)
        {
            foreach (String connectionId in GetPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("OpenWantToKnockModal");
            }
        }

        public async Task SendAskAnnounceHochzeit(SchafkopfHub hub)
        {
            if (GameState.AnnouncedGame == GameType.Hochzeit && GameState.PlayingPlayers.Any(p => p != GameState.Leader && !p.HasAnsweredMarriageOffer))
            {
                foreach (Player player in GameState.PlayingPlayers.Where(p => p != GameState.Leader && !p.HasAnsweredMarriageOffer))
                {
                    await SendAskWantToMarryPlayer(hub, player.GetConnectionIdsWithSpectators());
                }
                return;
            }

            foreach (Player player in GameState.PlayingPlayers)
            {
                if (GameState.Rules.isHochzeitEnabled && player.HandTrumpCount(GameType.Ramsch, Color.Herz) == 1 && !player.HasBeenAskedToOfferMarriage)
                {
                    foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                    {
                        await hub.Clients.Client(connectionId).SendAsync("AskAnnounceHochzeit");
                    }
                    return;
                }
            }

            GameState.AnnouncedGame = GameType.Ramsch;
            GameState.Leader = null;
            GameState.CurrentGameState = State.Announce;
            GameState.ActionPlayer = GameState.PlayingPlayers.IndexOf(GameState.Players[GameState.StartPlayer]);
            await SendPlayers(hub);
            await SendAskAnnounce(hub);
        }

        public async Task SendAskWantToMarryPlayer(SchafkopfHub hub, List<string> connectionIds)
        {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("AskWantToMarryPlayer", GameState.Leader.Name);
            }
        }

        public async Task SendAskExchangeCards(SchafkopfHub hub, List<string> connectionIds)
        {
            foreach (string connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "ReceiveInfo",
                    "Klicke auf die Karte, die du deinem Mitspieler geben willst."
                );
            }
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
            if (GameState.AnnouncedGame == GameType.Sauspiel || GameState.AnnouncedGame == GameType.Farbsolo || GameState.AnnouncedGame == GameType.FarbsoloTout)
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
                
                    // Resend revealed cards for BettelBrett to reconnecting player if needed
                    if (GameState.AnnouncedGame == GameType.BettelBrett && GameState.HasRevealedBettelBrettCards && GameState.Leader != null)
                    {
                        foreach (string connectionId in player.GetConnectionIds())
                        {
                            if (connectionIds.Contains(connectionId))
                            {
                                await hub.Clients.Client(connectionId).SendAsync(
                                    "RevealPlayerCards",
                                    GameState.Leader.Name, 
                                    GameState.Leader.GetHandCards().Select(c => new { Color = c.Color, Number = c.Number }).ToList()
                                );
                            }
                        }
                    }                // Resend revealed cards for BettelBrett if needed
                if (GameState.AnnouncedGame == GameType.BettelBrett && GameState.HasRevealedBettelBrettCards && GameState.Leader != null)
                {
                    await hub.Clients.Clients(connectionIds).SendAsync(
                        "RevealPlayerCards",
                        GameState.Leader.Name,
                        GameState.Leader.GetHandCards().Select(c => new { Color = c.Color, Number = c.Number }).ToList()
                    );
                }
                await player.SendHand(hub, GameState.AnnouncedGame, GameState.GetTrumpColor());
                await SendTakeTrickButton(hub, connectionIds);
                await SendUpdateContraButton(hub, connectionIds);
            }
            else if (GameState.CurrentGameState == State.Knock)
            {
                await player.SendHalfHand(hub);
            }
            else
            {
                await player.SendHand(hub);
                await SendStartPlayer(hub, connectionIds);
            }
            // send modals
            if (GameState.CurrentGameState == State.Playing && GameState.TrickCount == GameState.initial_number_of_cards_per_player)
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
            if (GameState.CurrentGameState == State.AnnounceHochzeit)
            {
                await SendAskAnnounceHochzeit(hub);
            }
            if (GameState.CurrentGameState == State.HochzeitExchangeCards && player == GameState.HusbandWife)
            {
                await SendAskExchangeCards(hub, connectionIds);
            }
            if (GameState.CurrentGameState == State.Knock)
            {
                await SendAskKnock(hub);
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
