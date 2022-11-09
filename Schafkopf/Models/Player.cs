using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;

namespace Schafkopf.Models
{
    public interface Player
    {
        string Name { get; }
        string Id { get; }
        Color AnnouncedColor { get; }
        int Balance { get; }
        Playing IsPlaying { get; }
        bool WantToPlay { get; }
        bool WantToPlayAnswered { get; }
        GameType AnnouncedGameType { get; }
        List<Player> SpectatorsWaitingForApproval { get; }
        int HandTrumpCount(GameType gameType, Color trump);
        Task SendHand(SchafkopfHub hub, GameType gameType = GameType.Ramsch, Color trump = Color.Herz);
        List<String> GetConnectionIds();
        List<String> GetConnectionIdsWithSpectators();
        List<Card> GetHandCards();
        string GetCurrentInfo(Game game);
        string GetSpectatorNames();
        bool IsSauspielPossible();
        Task<bool> IsSauspielOnColorPossible(Color searchedColor, SchafkopfHub hub);
        Task AskForApprovalToSpectate(SchafkopfHub hub);
        bool IsSpectators(Player player);
    }
    public class PlayerState : Player
    {
        public List<Card> HandCards = new List<Card>();
        private int _Balance = 0;
        public String _Name = "";
        public String _Id = "";
        private readonly List<String> _connectionIds = new List<String>();
        public Playing _IsPlaying = Playing.Undecided;
        public Boolean _WantToPlay = false;
        public Boolean _WantToPlayAnswered = false;
        public GameType _AnnouncedGameType = GameType.Ramsch;
        public Color _AnnouncedColor = Color.None;
        public List<PlayerState> Spectators = new List<PlayerState>();
        public Queue<PlayerState> _SpectatorsWaitingForApproval = new Queue<PlayerState>();
        private bool IsRunaway = false;

        public string Name => _Name;
        public string Id => _Id;
        public Color AnnouncedColor => _AnnouncedColor;
        public int Balance => _Balance;
        public Playing IsPlaying => _IsPlaying;
        public List<Player> SpectatorsWaitingForApproval => _SpectatorsWaitingForApproval.Cast<Player>().ToList();

        public bool WantToPlay => _WantToPlay;

        GameType Player.AnnouncedGameType => _AnnouncedGameType;

        public bool WantToPlayAnswered => _WantToPlayAnswered;

        public PlayerState(String name, String connectionId)
        {
            _Name = name;
            AddConnectionId(connectionId);
            _Id = System.Guid.NewGuid().ToString();
        }

        public void Reset()
        {
            HandCards = new List<Card>();
            _Balance = 0;
            _IsPlaying = Playing.Undecided;
            _WantToPlay = false;
            _WantToPlayAnswered = false;
            _AnnouncedGameType = GameType.Ramsch;
            _AnnouncedColor = Color.None;
            Spectators = new List<PlayerState>();
            _SpectatorsWaitingForApproval = new Queue<PlayerState>();
            IsRunaway = false;
        }

        //-------------------------------------------------
        // Player plays a card
        // Card will be removed from the hand-cards
        // Throw exception in case that a card has been played twice
        //-------------------------------------------------
        public (Card, string) PlayCard(Color cardColor, int cardNumber, SchafkopfHub hub, Game game)
        {
            foreach (Card card in HandCards)
            {
                if (card.Color == cardColor && card.Number == cardNumber)
                {
                    if (!CanCardBePlayed(game, card))
                    {
                        string message = "Die Karte kannst du gerade nicht spielen!";
                        return (null, message);
                    }
                    HandCards.Remove(card);
                    return (card, "");
                }
            }
            throw new Exception("There is something wrong, the card is not on the hand.");
        }

        //-------------------------------------------------
        // Player takes the trick and add its points to his own balance
        //-------------------------------------------------
        public void AddPoints(int points)
        {
            _Balance += points;
        }

        public void Announce(bool wantToPlay)
        {
            _WantToPlay = wantToPlay;
            _WantToPlayAnswered = true;
        }

        //-------------------------------------------------
        // Player can decide what type of Game he is playing
        //-------------------------------------------------
        public void AnnounceGameType(GameType gameType)
        {
            _AnnouncedGameType = gameType;
        }

        public void AddConnectionId(String connectionId)
        {
            lock (_connectionIds)
            {
                _connectionIds.Add(connectionId);
            }
        }
        public bool RemoveConnectionId(String id)
        {
            lock (_connectionIds)
            {
                return _connectionIds.Remove(id);
            }
        }
        public List<String> GetConnectionIds()
        {
            return _connectionIds.ToList();
        }
        public List<String> GetConnectionIdsWithSpectators()
        {
            return GetSpectatorConnectionIds().Concat(GetConnectionIds()).ToList();
        }

        public async Task SendHand(SchafkopfHub hub, GameType gameType = GameType.Ramsch, Color trump = Color.Herz)
        {
            foreach (String connectionId in GetConnectionIdsWithSpectators())
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "ReceiveHand",
                    HandCards.OrderByDescending(c => c.GetValue(gameType, trump)).Select(card => card.ToString())
                );
            }
        }

        public List<String> GetSpectatorConnectionIds()
        {
            return Spectators.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIds()).ToList());
        }

        public void AddSpectator(PlayerState player)
        {
            Spectators.Add(player);
        }

        public async Task AskForApprovalToSpectate(SchafkopfHub hub)
        {
            if (_SpectatorsWaitingForApproval.Count == 0)
            {
                foreach (String connectionId in GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("CloseAllowSpectatorModal");
                }
                return;
            }
            foreach (String connectionId in GetConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("AskAllowSpectator", _SpectatorsWaitingForApproval.Peek()._Name);
            }
        }

        private bool CanCardBePlayed(Game game, Card card)
        {
            if (game.GameState.Trick.FirstCard == null)
            {
                if (game.GameState.AnnouncedGame == GameType.Sauspiel &&
                    HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor) &&
                    !IsRunaway)
                {
                    // Davonlaufen
                    if (HandColorCount(game.GameState.Leader.AnnouncedColor, game.GameState.AnnouncedGame, game.GameState.GetTrumpColor()) >= 4)
                    {
                        IsRunaway = true;
                        return true;
                    }
                    return card.IsTrump(game.GameState.AnnouncedGame, game.GameState.GetTrumpColor()) || card.Color != game.GameState.Leader.AnnouncedColor || card.Number == 11;
                }
                return true;
            }
            else if (game.GameState.Trick.FirstCard.IsTrump(game.GameState.AnnouncedGame, game.GameState.GetTrumpColor()))
            {
                if (HandContainsTrump(game))
                {
                    return card.IsTrump(game.GameState.AnnouncedGame, game.GameState.GetTrumpColor());
                }
                if (
                    game.GameState.AnnouncedGame == GameType.Sauspiel &&
                    HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor) &&
                    !IsRunaway
                )
                {
                    if (game.GameState.TrickCount < 6)
                    {
                        return card.Color != game.GameState.Leader.AnnouncedColor || card.Number != 11;
                    }
                }
                return true;
            }
            else if (HandContainsColor(game.GameState.Trick.FirstCard.Color, game.GameState.AnnouncedGame, game.GameState.GetTrumpColor()))
            {
                if (
                    game.GameState.AnnouncedGame == GameType.Sauspiel &&
                    game.GameState.Trick.FirstCard.Color == game.GameState.Leader.AnnouncedColor &&
                    HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor)
                )
                {
                    return card.Color == game.GameState.Trick.FirstCard.Color && card.Number == 11;
                }
                return !card.IsTrump(game.GameState.AnnouncedGame, game.GameState.GetTrumpColor()) && card.Color == game.GameState.Trick.FirstCard.Color;
            }
            else if (
                game.GameState.AnnouncedGame == GameType.Sauspiel &&
                HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor) &&
                !IsRunaway
            )
            {
                if (game.GameState.TrickCount < 6)
                {
                    return card.Color != game.GameState.Leader.AnnouncedColor || card.Number != 11;
                }
            }
            return true;
        }

        private int HandColorCount(Color color, GameType gameType, Color trump)
        {
            return HandCards.Where(
                        c => !c.IsTrump(gameType, trump) &&
                        c.Color == color
                    ).ToList().Count;
        }

        public int HandTrumpCount(GameType gameType, Color trump)
        {
            return HandCards.Where(c => c.IsTrump(gameType, trump)).ToList().Count;
        }

        private bool HandContainsColor(Color color, GameType gameType, Color trump)
        {
            return HandCards.Any(c => !c.IsTrump(gameType, trump) && c.Color == color);
        }

        private bool HandContainsTrump(Game game)
        {
            return HandCards.Any(c => c.IsTrump(game.GameState.AnnouncedGame, game.GameState.GetTrumpColor()));
        }

        private bool HandContainsSearchedSau(Color searchedColor)
        {
            return HandCards.Any(c => c.Color == searchedColor && c.Number == 11);
        }

        public bool IsSauspielPossible()
        {
            foreach (Color searchedColor in new List<Color>() { Color.Eichel, Color.Gras, Color.Schellen })
            {
                if (
                    HandContainsColor(searchedColor, GameType.Sauspiel, Color.Herz) &&
                    !HandContainsSearchedSau(searchedColor)
                )
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> IsSauspielOnColorPossible(Color searchedColor, SchafkopfHub hub)
        {
            if (searchedColor == Color.Herz)
            {
                foreach (String connectionId in GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("ReceiveError", "Du kannst die Herz-Sau nicht suchen!");
                }
                return false;
            }
            if (
                   HandContainsColor(searchedColor, GameType.Sauspiel, Color.Herz) &&
                   !HandContainsSearchedSau(searchedColor)
               )
            {
                return true;
            }
            foreach (String connectionId in GetConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveError", $"Du kannst nicht auf die {searchedColor}-Sau spielen!");
            }
            return false;
        }

        public string GetSpectatorNames()
        {
            if (Spectators.Where(s => s.GetConnectionIds().Count > 0).ToList().Count > 0)
            {
                return $" ({String.Join(", ", Spectators.Where(s => s.GetConnectionIds().Count > 0).Select(s => s._Name))})";
            }
            return "";
        }

        public string GetCurrentInfo(Game game)
        {
            Random rnd = new Random();
            if (game.GameState.CurrentGameState == State.AnnounceGameColor || (game.GameState.CurrentGameState == State.AnnounceGameType && _AnnouncedGameType != GameType.Ramsch))
            {
                switch (_AnnouncedGameType)
                {
                    case GameType.Farbsolo:
                        return "Ich hab ein Solo";
                    case GameType.Bettel:
                        return "Ich spiel' an Bettel";
                    case GameType.Wenz:
                        return "Ich hab ein Wenz";
                    case GameType.Geier:
                        return "Ich spiel' an Geier";
                    case GameType.Sauspiel:
                        return "Ich hab ein Sauspiel";
                }
            }
            else if (game.GameState.CurrentGameState == State.AnnounceGameType || (game.GameState.CurrentGameState == State.Announce && _WantToPlayAnswered))
            {
                if (_WantToPlay)
                {
                    string[] returnvalues = {"Dat", "Ich dat", "Ich würde", "Dat scho", "Ich hab was"};
                    int index = rnd.Next(returnvalues.Length);
                    return returnvalues[index];
                }
                else
                {
                    string[] returnvalues = {"Weiter", "Nix", "Weg", "Fuera", "Nein", "Naa"};
                    int index = rnd.Next(returnvalues.Length);
                    return returnvalues[index];
                }
            }
            return "";
        }

        public List<Card> GetHandCards()
        {
            return HandCards.ToList();
        }

        public bool IsSpectators(Player player) {
            return Spectators.Contains(player);
        }
    }
}
