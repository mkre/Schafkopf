using Microsoft.AspNetCore.SignalR;
using Schafkopf.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using Schafkopf.GameState;

namespace Schafkopf.Hubs
{
    public class SchafkopfHub : Hub
    {
        public static Dictionary<String, Game> Games = new Dictionary<String, Game>();
        public async Task SendChatMessage(string message)
        {
            Game game = ((Game)Context.Items["game"]);
            if (message == "/restart")
            {
                await game.Reset(this);
                return;
            }
            String user = ((Player)Context.Items["player"]).Name;
            if (message.StartsWith("/kick"))
            {
                Player playerToKick = game.GameState.Players.Single(p => p.Name == message.Split("/kick ")[1]);
                foreach (String connectionId in playerToKick.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveKicked", user);
                }
                foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveInfo", $"{user} hat {playerToKick.Name} rausgeworfen");
                }
                return;
            }
            foreach (String connectionId in game.GetPlayersConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("ReceiveChatMessage", user, message);
            }
        }

        public async Task Knock(bool wantToKnock)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (game.GameState.CurrentGameState == State.Knock)
            {
                if (wantToKnock)
                {
                    foreach (String connectionId in game.GetPlayersConnectionIds())
                    {
                        await Clients.Client(connectionId).SendAsync(
                            "ReceiveGameInfo",
                            $"{player.Name} klopft"
                        );
                    }
                }
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    await Clients.Client(connectionId).SendAsync("CloseKnockModal");
                }
                game.GameState.Knock(player, wantToKnock);
                await game.SendPlayers(this);
                if (game.GameState.PlayingPlayers.All(p => p.WantToKnockAnswered))
                {
                    game.GameState.CurrentGameState = State.AnnounceHochzeit;
                    await game.FinalizeDealCards(this);
                    return;
                }
            }
        }

        public async Task Announce(bool wantToPlay)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (game.GameState.CurrentGameState == State.Announce && player == game.GameState.PlayingPlayers[game.GameState.ActionPlayer])
            {
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                }
                game.GameState.Announce(wantToPlay);
                game.GameState.IncrementActionPlayer();
                await game.SendPlayers(this);
                if (game.GameState.PlayingPlayers.All(p => p.WantToPlayAnswered))
                {
                    game.GameState.CurrentGameState = State.AnnounceGameType;
                    await game.SendAskForGameType(this);
                    return;
                }
                await game.SendAskAnnounce(this);
            }
            if (game.GameState.CurrentGameState == State.AnnounceHochzeit)
            {
                if (player.HandTrumpCount(GameType.Ramsch, Color.Herz) == 1 && !player.HasBeenAskedToOfferMarriage)
                {
                    foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                    {
                        await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                    }
                    game.GameState.SetPlayerHasBeenAskedToOfferMarriage(true, player);
                    if (wantToPlay)
                    {
                        game.GameState.AnnouncedGame = GameType.Hochzeit;
                        game.GameState.Leader = player;
                    }
                    await game.SendAskAnnounceHochzeit(this);
                }
                else if (game.GameState.AnnouncedGame == GameType.Hochzeit && !player.HasAnsweredMarriageOffer)
                {
                    game.GameState.SetPlayerHasAnsweredMarriageOffer(true, player);
                    if (wantToPlay)
                    {
                        foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                        }
                        game.GameState.CurrentGameState = State.HochzeitExchangeCards;
                        game.GameState.HusbandWife = player;
                        game.GameState.ActionPlayer = game.GameState.PlayingPlayers.IndexOf(game.GameState.HusbandWife);
                        await game.SendAskExchangeCards(this, game.GameState.HusbandWife.GetConnectionIdsWithSpectators());
                    }
                    else
                    {
                        foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                        }
                        await game.SendAskAnnounceHochzeit(this);
                    }
                }
                await game.SendPlayers(this);
            }
        }

        public async Task AnnounceGameType(string gameTypeString)
        {
            Game game = ((Game)Context.Items["game"]);
            if (game.GameState.CurrentGameState != State.AnnounceGameType)
            {
                return;
            }
            GameType gameType = GameType.Ramsch;
            switch (gameTypeString)
            {
                case "Sauspiel":
                    gameType = GameType.Sauspiel;
                    break;
                case "Geier":
                    gameType = GameType.Geier;
                    break;
                case "Wenz":
                    gameType = GameType.Wenz;
                    break;
                case "Bettel":
                    gameType = GameType.Bettel;
                    break;
                case "Solo":
                    gameType = GameType.Farbsolo;
                    break;
                case "Geier-Tout":
                    gameType = GameType.GeierTout;
                    break;
                case "Wenz-Tout":
                    gameType = GameType.WenzTout;
                    break;
                case "Solo-Tout":
                    gameType = GameType.FarbsoloTout;
                    break;
                case "BettelBrett":
                    gameType = GameType.BettelBrett;
                    break;
            }
            Player player = (Player)Context.Items["player"];
            if (player == game.GameState.PlayingPlayers[game.GameState.ActionPlayer])
            {
                if (gameType == GameType.Sauspiel && !player.IsSauspielPossible())
                {
                    foreach (String connectionId in player.GetConnectionIds())
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveError", "Du darfst kein Sauspiel ansagen!");
                    }
                    return;
                }
                game.GameState.AnnounceGameType(gameType);
                game.GameState.IncrementActionPlayer();
                await game.SendPlayers(this);
                await game.SendAskForGameType(this);
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    await Clients.Client(connectionId).SendAsync("CloseAnnounceGameTypeModal");
                }
            }
        }
        public async Task AnnounceGameColor(string gameColorString)
        {
            Game game = ((Game)Context.Items["game"]);
            if (game.GameState.CurrentGameState != State.AnnounceGameColor)
            {
                return;
            }
            Player player = (Player)Context.Items["player"];
            if (player != game.GameState.Leader)
            {
                return;
            }
            Color color = Color.Eichel;
            switch (gameColorString)
            {
                case "Eichel":
                    color = Color.Eichel;
                    break;
                case "Gras":
                    color = Color.Gras;
                    break;
                case "Herz":
                    color = Color.Herz;
                    break;
                case "Schellen":
                    color = Color.Schellen;
                    break;
            }
            if (game.GameState.AnnouncedGame == GameType.Sauspiel)
            {
                if (!await player.IsSauspielOnColorPossible(color, this))
                {
                    return;
                }
            }
            game.GameState.SetAnnouncedColor(color);
            await game.StartGame(this);
            foreach (String connectionId in player.GetConnectionIdsWithSpectators())
            {
                await Clients.Client(connectionId).SendAsync("CloseGameColorModal");
            }
        }

        ///<summary>Method <c>AnnounceContraReSup</c> is called when any player presses the Contra/Re/Sup button</summary>
        public async Task AnnounceContraReSup()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];

            //It's only allowed to announce Contra/Re/Sup/... if several prerequisites are fulfilled
            if (game.GameState.CurrentGameState == State.Playing && game.GameState.AllowedToAnnounceContraPlayers.Contains(player) && game.GameState.TrickCount == 0 && game.GameState.Trick.Count < game.GameState.Rules.contraMustBeSaidBeforeTrickCard && game.GameState.AllowedToAnnounceContraPlayers.Contains(player) && game.GameState.CurrentContraState < Enum.GetValues(typeof(ContraState)).Cast<ContraState>().Max())
            {
                ++game.GameState.CurrentContraState;
                game.GameState.CurrentContraAnnouncer = player;
                
                //Special case Ramsch: No teams, every player can announce only once
                if (game.GameState.AnnouncedGame == GameType.Ramsch)
                {
                    game.GameState.RemovePlayerFromAllowedToAnnounceContraPlayers(player);
                }
                else
                {
                    game.GameState.AllowedToAnnounceContraPlayers.Clear();
                    if (game.GameState.Group0.Contains(player)) 
                    {
                        game.GameState.AddPlayerToAllowedToAnnounceContraPlayers(game.GameState.Group1);
                    }
                    else
                    {
                        game.GameState.AddPlayerToAllowedToAnnounceContraPlayers(game.GameState.Group0);
                    }
                }

                foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync(
                        "ReceiveGameInfo",
                        $"{game.GameState.CurrentContraAnnouncer.Name.ToString()} sagt ein {game.GameState.CurrentContraState}"
                    );

                }
                await game.SendUpdateContraButton(this, game.GetPlayingPlayersConnectionIds());                
            }
        }

        public async Task PlayCard(String cardId)
        {
            Game game = ((Game)Context.Items["game"]);
            Color cardColor;
            Enum.TryParse(cardId.Split("-")[0], true, out cardColor);
            int cardNumber = Int16.Parse(cardId.Split("-")[1]);
            await game.PlayCard((Player)Context.Items["player"], cardColor, cardNumber, this);
        }


        public async Task ReconnectPlayer(string userId, string gameId)
        {
            if (gameId == null || !Games.Keys.Contains(gameId))
            {
                await Clients.Caller.SendAsync("AskUsername");
                return;
            }
            Game game = Games[gameId];
            if (!game.GameState.Players.Any(p => p.Id == userId))
            {
                await Clients.Caller.SendAsync("AskUsername");
                return;
            }
            Context.Items.Add("game", game);
            Player player = game.GameState.Players.Single(p => p.Id == userId);
            game.GameState.AddPlayerConnectionId(Context.ConnectionId, player);
            Context.Items.Add("player", player);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Willkommen zurück, {player.Name}!");
            await game.SendPlayersInfo(this);
            await game.SendPlayers(this);
            if (game.GameState.CurrentGameState != State.Idle)
            {
                if (game.GameState.PlayingPlayers.Contains(player))
                {
                    await game.SendUpdatedGameState(player, this, new List<String> { Context.ConnectionId });
                    // check if all players are connected again and close connectionLostModal for the other players
                    if (game.GameState.PlayingPlayers.All(p => p.GetConnectionIds().Count > 0))
                    {
                        foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseGameOverModal");
                        }
                    }
                }
                else if (game.GameState.PlayingPlayers.Any(p => p.IsSpectators(player)))
                {
                    await game.SendUpdatedGameState(
                        game.GameState.PlayingPlayers.Single(p => p.IsSpectators(player)),
                        this,
                        new List<String> { Context.ConnectionId });
                }
                else
                {
                    await game.SendAskWantToSpectate(this, new List<string>() { Context.ConnectionId });
                }
            }
            else
            {
                if (!game.GameState.PlayingPlayers.Contains(player))
                {
                    if (game.GameState.Players.Where((p => p.GetConnectionIds().Count > 0 && p.IsPlaying == Playing.Play)).ToList().Count <= 4)
                    {
                        await game.PlayerPlaysTheGame(player, this);
                    }
                    else
                    {
                        await game.SendAskWantToPlay(this, new List<string>() { Context.ConnectionId });
                    }
                }
            }
        }

        public async Task CreateGame(string gameId, bool isShortHand, bool withBettel, bool withHochzeit, bool withKlopfen)
        {
            Game game;
            if (Games.Keys.Contains(gameId))
            {
                await Clients.Caller.SendAsync("ReceiveError", "Tisch existiert bereits!");
                return;
            }
            else
            {
                GameRules rules = new GameRules();
                rules.isShortHand = isShortHand;
                rules.isBettelEnabled = withBettel;
                rules.isHochzeitEnabled = withHochzeit;
                rules.isKlopfenEnabled = withKlopfen;
                rules.contraMustBeSaidBeforeTrickCard = 2;
                game = new Game(rules);
                Games[gameId] = game;
            }
        }

        public bool GameExists(string gameId)
        {
            return Games.Keys.Contains(gameId);
        }

        public async Task AddPlayer(string userName, string gameId)
        {
            Game game;
            Player player;
            String error = "";
            if (userName.ToLower() == "system")
            {
                error = "Dein Name darf nicht \"System\" sein!";
            }
            else if (userName.Trim() == "")
            {
                error = "Dein Name darf nicht leer sein!";
            }
            else if (userName.Length > 20)
            {
                error = "Dein Name darf nicht länger als 20 Zeichen sein!";
            }
            // rename user
            if (Context.Items.Keys.Contains("player"))
            {
                player = (Player)Context.Items["player"];
                game = (Game)Context.Items["game"];
                if (userName != player.Name && game.GameState.Players.Where(p => p.Name == userName).ToList().Count > 0)
                {
                    error = $"Der Name \"{userName}\" ist bereits vergeben!";
                }
                if (error != "")
                {
                    await Clients.Caller.SendAsync("ReceiveError", $"{error}");
                    return;
                }
                game.GameState.SetPlayerName(userName, player);
                await Clients.Caller.SendAsync("StoreUser", player.Id, player.Name);
                await game.SendPlayers(this);
                return;
            }

            if (Games.Keys.Contains(gameId))
            {
                game = Games[gameId];
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveError", "Der Tisch existiert nicht!");
                return;
            }
            if (game.GameState.Players.Where(p => p.Name == userName).ToList().Count > 0)
            {
                player = game.GameState.Players.Single(p => p.Name == userName);
                // Assume identity of existing user if it has no more active connections
                if (player.GetConnectionIds().Count == 0)
                {
                    string newUserId = System.Guid.NewGuid().ToString();
                    game.GameState.SetPlayerId(newUserId, player);
                    await Clients.Caller.SendAsync("StoreUser", player.Id, player.Name);
                    await ReconnectPlayer(newUserId, gameId);
                    return;
                }
                error = $"Der Name \"{userName}\" ist bereits vergeben!";
            }
            if (error != "")
            {
                await Clients.Caller.SendAsync("ReceiveError", $"Error: {error}");
                return;
            }
            Context.Items.Add("game", game);
            player = game.GameState.AddPlayer(userName, Context.ConnectionId);
            Context.Items.Add("player", player);
            await Clients.Caller.SendAsync("StoreUser", player.Id, player.Name);
            await game.AddPlayer(player, this);
        }

        public async Task PlayerWantsToPlay()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            await game.PlayerPlaysTheGame(player, this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
        }
        public async Task PlayerWantsToSpectate(int playerId)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToSpectateModal");
            }
            if (playerId >= 0 && playerId <= 4)
            {
                game.GameState.EnqueueSpectatorForApproval(game.GameState.PlayingPlayers[playerId], player);
                await game.GameState.PlayingPlayers[playerId].AskForApprovalToSpectate(this);
            }
        }

        public async Task AllowSpectator(bool allow)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (player.SpectatorsWaitingForApproval.Count == 0)
            {
                return;
            }
            Player spectator = game.GameState.DequeueSpectator(player, allow);
            if (allow)
            {
                await game.SendUpdatedGameState(player, this, spectator.GetConnectionIds());
            }
            else
            {
                await game.SendAskWantToSpectate(this, spectator.GetConnectionIds());
                foreach (String connectionId in spectator.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync(
                        "ReceiveError",
                        $"{player.Name} will nicht, dass du zuschaust"
                    );
                }
            }
            await player.AskForApprovalToSpectate(this);
        }

        public async Task PlayerWantsToPause()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            await game.PlayerDoesNotPlayTheGame(player, this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
        }

        public async Task ResetGame()
        {
            Game game = ((Game)Context.Items["game"]);
            await game.Reset(this);
        }

        public async Task ShowLastTrick(bool showLastTrick)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (showLastTrick && game.GameState.LastTrick != null)
            {
                await game.GameState.LastTrick.SendTrick(this, game, new List<string>() { Context.ConnectionId });
                await game.SendLastTrickButton(this, new List<string>() { Context.ConnectionId }, LastTrickButtonState.hide);
            }
            else
            {
                await game.GameState.Trick.SendTrick(this, game, new List<string>() { Context.ConnectionId });
                await game.SendLastTrickButton(this, new List<string>() { Context.ConnectionId }, LastTrickButtonState.show);
            }
        }

        public async Task TakeTrick()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (game.GameState.Trick.Count == 4 && game.GameState.Trick.Winner == player)
            {
                game.GameState.TakeTrick();
                
                // For BettelBrett, reveal the leader's cards after the first trick
                if (game.GameState.AnnouncedGame == GameType.BettelBrett && game.GameState.TrickCount == 1)
                {
                    game.GameState.SetBettelBrettCardsRevealed();
                    // Send the leader's cards to all players
                    foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                    {
                        await Clients.Client(connectionId).SendAsync(
                            "RevealPlayerCards",
                            game.GameState.Leader.Name,
                            game.GameState.Leader.GetHandCards().Select(c => new { Color = c.Color, Number = c.Number }).ToList()
                        );
                    }
                }

                if (game.GameState.TrickCount == game.GameState.initial_number_of_cards_per_player)
                {
                    await game.SendEndGameModal(this, game.GetPlayingPlayersConnectionIds());
                }

                game.GameState.ActionPlayer = game.GameState.PlayingPlayers.FindIndex(p => p == player);
                await game.SendPlayers(this);
                game.GameState.NewTrick();
                await game.GameState.Trick.SendTrick(this, game, game.GetPlayingPlayersConnectionIds());
                await game.SendTakeTrickButton(this, game.GetPlayingPlayersConnectionIds());
                if (game.GameState.LastTrick != null)
                {
                    await game.SendLastTrickButton(this, game.GetPlayingPlayersConnectionIds(), LastTrickButtonState.show);
                }
            }
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (Context.Items.Keys.Contains("game") && Context.Items.Keys.Contains("player"))
            {
                Game game = ((Game)Context.Items["game"]);
                foreach (Player player in game.GameState.Players)
                {
                    if (game.GameState.RemovePlayerConnectionId(Context.ConnectionId, player) && player.GetConnectionIds().Count == 0)
                    {
                        Task asyncTask = game.SendPlayersInfo(this);
                        asyncTask = game.SendPlayers(this);
                        asyncTask = game.PlayerDoesNotPlayTheGame(player, this);
                        if (game.GameState.PlayingPlayers.Contains(player))
                        {
                            if (game.GameState.CurrentGameState != State.Idle)
                            {
                                asyncTask = game.SendConnectionToPlayerLostModal(this, game.GetPlayingPlayersConnectionIds());
                                asyncTask = game.ResetIfAllConnectionsLost(this);
                            }
                        }
                    }
                }
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
}