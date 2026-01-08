using System;
using System.Collections.Generic;
using System.Linq;
using Schafkopf.Hubs;
using Schafkopf.Models;

namespace Schafkopf.GameState
{
    public class GameState
    {
        // internal Gamestate is private, so it can not be accesses by other classes
        private readonly List<PlayerState> _Players = new List<PlayerState>();
        private List<PlayerState> _PlayingPlayers = new List<PlayerState>();
        private readonly Carddeck _Carddeck;
        private int _initial_number_of_cards_per_player = 0;
        private State _CurrentGameState = State.Idle;
        private int[] _Groups = new int[] { 0, 0, 0, 0 };
        private List<Player> _Group0 = new List<Player>();
        private List<Player> _Group1 = new List<Player>();
        private int _StartPlayer = -1;
        private int _ActionPlayer = -1;
        private GameType _AnnouncedGame = GameType.Ramsch;
        private PlayerState _HusbandWife = null;
        private PlayerState _Leader;
        private TrickState _Trick = null;
        private TrickState _LastTrick = null;
        private int _TrickCount = 0;
        public bool HasRevealedBettelBrettCards => _HasRevealedBettelBrettCards;
        private bool _HasRevealedBettelBrettCards = false;

        internal void SetBettelBrettCardsRevealed()
        {
            lock (_Lock)
            {
                _HasRevealedBettelBrettCards = true;
            }
        }
        private ContraState _CurrentContraState = ContraState.Null;
        private List<Player> _AllowedToAnnounceContraPlayers = new List<Player>();
        private Player _CurrentContraAnnouncer = null;
        private readonly object _Lock = new object();

        // public access is either read-only or synchronized through _Lock
        public int StartPlayer => _StartPlayer;
        public Trick Trick => _Trick;
        public Trick LastTrick => _LastTrick;
        public List<Player> Players => _Players.Cast<Player>().ToList();
        public List<Player> PlayingPlayers => _PlayingPlayers.Cast<Player>().ToList();
        public List<Player> AllowedToAnnounceContraPlayers => _AllowedToAnnounceContraPlayers;

        public readonly GameRules Rules;

        public GameState(GameRules rules) {
            Rules = rules;
            _Carddeck = new Carddeck(rules.isShortHand);
        }
        public int ActionPlayer
        {
            get => _ActionPlayer;
            set
            {
                lock (_Lock)
                {
                    _ActionPlayer = value;
                }
            }
        }
        public GameType AnnouncedGame
        {
            get => _AnnouncedGame;
            set
            {
                lock (_Lock)
                {
                    _AnnouncedGame = value;
                }
            }
        }
        public int initial_number_of_cards_per_player => _initial_number_of_cards_per_player;
        public int TrickCount => _TrickCount;
        public State CurrentGameState
        {
            get => _CurrentGameState;
            set
            {
                lock (_Lock)
                {
                    _CurrentGameState = value;
                }
            }
        }

        public ContraState CurrentContraState
        {
            get => _CurrentContraState;
            set
            {
                lock (_Lock)
                {
                    _CurrentContraState = value;
                }
            }
        }

        public Player CurrentContraAnnouncer
        {
            get => _CurrentContraAnnouncer;
            set
            {
                lock (_Lock)
                {
                    _CurrentContraAnnouncer = value;
                }
            }
        }

        public Player Leader
        {
            get => _Leader;
            set
            {
                lock (_Lock)
                {
                    if (value == null)
                    {
                        _Leader = null;
                    }
                    else
                    {
                        _Leader = _Players.Single(p => p.Id == value.Id);
                    }
                }
            }
        }
        public Player HusbandWife
        {
            get => _HusbandWife;
            set
            {
                lock (_Lock)
                {
                    if (value == null)
                    {
                        _HusbandWife = null;
                    }
                    else
                    {
                        _HusbandWife = _Players.Single(p => p.Id == value.Id);
                    }
                }
            }
        }

        public List<Player> Group0
        {
            get => _Group0;
        }

        public List<Player> Group1
        {
            get => _Group1;
        }

        public Color GetTrumpColor()
        {
            switch (_AnnouncedGame)
            {
                case GameType.Ramsch:
                case GameType.Sauspiel:
                case GameType.Hochzeit:
                    return Color.Herz;
                case GameType.Farbsolo:
                case GameType.FarbsoloTout:
                    return Leader.AnnouncedColor;
                case GameType.Wenz:
                case GameType.WenzTout:
                case GameType.Geier:
                case GameType.GeierTout:
                case GameType.Bettel:
                case GameType.BettelBrett:
                default:
                    return Color.None;
            }
        }
        internal (int leaderPoints, int followerPoints, string leaderNames, string followerNames ) GetFinalPointsAndTeams()
        {
            int leaderPoints = 0;
            int followerPoints = 0;
            string leaderNames = "";
            string followerNames = "";
            for (int i = 0; i < 4; i++)
            {
                if (_Groups[i] == 0)
                {
                    followerPoints += _PlayingPlayers[i].Balance;
                    if (string.IsNullOrEmpty(followerNames))
                    {
                        followerNames = _PlayingPlayers[i].Name;
                    }
                    else
                    {
                        followerNames += ", " + _PlayingPlayers[i].Name;
                    }
                }
                else
                {
                    leaderPoints += _PlayingPlayers[i].Balance;
                    if (string.IsNullOrEmpty(leaderNames))
                    {
                        leaderNames = _PlayingPlayers[i].Name;
                    }
                    else
                    {
                        leaderNames += ", " + _PlayingPlayers[i].Name;
                    }
                }
            }
            return (leaderPoints, followerPoints, leaderNames, followerNames);
        }
        internal void IncrementActionPlayer()
        {
            lock (_Lock)
            {
                _ActionPlayer = (_ActionPlayer + 1) % 4;
            }
        }
        public void AddCardToTrick(Card card, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                _Trick.AddCard(card, playerState);
            }
        }

        internal void Announce(bool wantToPlay)
        {
            lock (_Lock)
            {
                _PlayingPlayers[_ActionPlayer].Announce(wantToPlay);
            }
        }

        internal void Knock(Player player, bool wantToKnock)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState.Knock(wantToKnock);
            }
        }

        public void NewTrick()
        {
            lock (_Lock)
            {
                if (_Trick != null)
                {
                    _LastTrick = _Trick;
                }
                _Trick = new TrickState(_AnnouncedGame, GetTrumpColor(), _ActionPlayer);
            }
        }

        public void Reset()
        {
            lock (_Lock)
            {
                _CurrentGameState = State.Idle;
                _Groups = new int[] { 0, 0, 0, 0 };
                _Group0 = new List<Player>();
                _Group1 = new List<Player>();
                _AnnouncedGame = GameType.Ramsch;
                Leader = null;
                HusbandWife = null;
                _Trick = null;
                _LastTrick = null;
                _TrickCount = 0;
                _HasRevealedBettelBrettCards = false;
                _ActionPlayer = -1;
                _PlayingPlayers = new List<PlayerState>();
                _CurrentContraState = ContraState.Null;
                _CurrentContraAnnouncer = null;
                _AllowedToAnnounceContraPlayers = new List<Player>();

                foreach (PlayerState player in _Players)
                {
                    player.Reset();
                }
            }
        }

        internal void StartGame()
        {
            lock (_Lock)
            {
                _CurrentGameState = State.AnnounceHochzeit;

                //New first player
                _StartPlayer = (_StartPlayer + 1) % Players.Count;
                while (!PlayingPlayers.Contains(Players[_StartPlayer]))
                {
                    _StartPlayer = (_StartPlayer + 1) % Players.Count;
                }    
                //Shuffle cards
                Card[] shuffledCards = _Carddeck.Shuffle();
                
                //// TESTING
                // Card[] shuffledCards = _Carddeck.Hochzeit();

                _initial_number_of_cards_per_player = shuffledCards.Length / 4;
                //Distribute cards to the players
                //Player 1 gets first 8 cards, Player 2 gets second 8 cards, an so on ...
                for (int i = 0; i < 4; i++)
                {
                    Card[] HandCards = new Card[_initial_number_of_cards_per_player];
                    for (int j = i * _initial_number_of_cards_per_player; j < (i + 1) * _initial_number_of_cards_per_player; j++)
                    {
                        HandCards[j % _initial_number_of_cards_per_player] = shuffledCards[j];
                    }
                    _PlayingPlayers[i].HandCards = new List<Card>(HandCards);
                }
            }
        }

        internal bool ExchangeCardWithPlayer(Player player, Color cardColor, int cardNumber, Player leader, SchafkopfHub hub, Game game)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                PlayerState leaderState = _Players.Single(p => p.Id == leader.Id);
                return playerState.ExchangeCardWithPlayer(cardColor, cardNumber, leaderState, hub, game);
            }
        }

        internal void SetPlayerPlaying(Playing value, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._IsPlaying = value;

                if (value == Playing.Play)
                {
                    if (!_PlayingPlayers.Contains(playerState))
                    {
                        for (int i = 0; i < _PlayingPlayers.Count; i++)
                        {
                            if (_Players.IndexOf(_PlayingPlayers[i]) > _Players.IndexOf(playerState))
                            {
                                _PlayingPlayers.Insert(i, playerState);
                                break;
                            }
                        }
                        if (!_PlayingPlayers.Contains(playerState))
                        {
                            _PlayingPlayers.Add(playerState);
                        }
                    }
                }
                else if (value == Playing.Pause)
                {
                    if (_PlayingPlayers.Contains(playerState))
                    {
                        _PlayingPlayers.Remove(playerState);
                    }
                }
            }
        }
        internal void SetPlayerHasBeenAskedToOfferMarriage(bool value, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._HasBeenAskedToOfferMarriage = value;
            }
        }

        internal void SetPlayerHasAnsweredMarriageOffer(bool value, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._HasAnsweredMarriageOffer = value;
            }
        }
        internal (Card, string) PlayCard(Color cardColor, int cardNumber, SchafkopfHub hub, Game game, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                return playerState.PlayCard(cardColor, cardNumber, hub, game);
            }
        }

        internal void AnnounceGameType(GameType gameType)
        {
            lock (_Lock)
            {
                PlayerState player = _PlayingPlayers[_ActionPlayer];
                player.AnnounceGameType(gameType);
            }
        }

        internal void SetAnnouncedColor(Color color)
        {
            lock (_Lock)
            {
                _Leader._AnnouncedColor = color;
            }
        }

        internal void AddPlayerConnectionId(string connectionId, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState.AddConnectionId(connectionId);
            }
        }

        internal bool RemovePlayerConnectionId(string connectionId, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                return playerState.RemoveConnectionId(connectionId);
            }
        }

        internal void SetPlayerName(string userName, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._Name = userName;
            }
        }

        internal void SetPlayerId(string newUserId, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._Id = newUserId;
            }
        }

        internal Player AddPlayer(string userName, string connectionId)
        {
            lock (_Lock)
            {
                PlayerState player = new PlayerState(userName, connectionId);
                _Players.Add(player);
                return player;
            }
        }

        internal void AddPlayerToAllowedToAnnounceContraPlayers (Player player)
        {
            lock (_Lock)
            {
                _AllowedToAnnounceContraPlayers.Add(player);
            }
        }

        internal void AddPlayerToAllowedToAnnounceContraPlayers (List<Player> players)
        {
            foreach (Player player in players) {
                AddPlayerToAllowedToAnnounceContraPlayers(player);
            }
        }

        internal bool RemovePlayerFromAllowedToAnnounceContraPlayers (Player player)
        {
            lock (_Lock)
            {
                if (_AllowedToAnnounceContraPlayers.Contains(player))
                {
                    _AllowedToAnnounceContraPlayers.Remove(player);
                    return true;
                } else
                {
                    return false;
                }
            }
        }

        internal bool RemovePlayerFromAllowedToAnnounceContraPlayers (List<Player> players)
        {
            List<bool> results = new List<bool>();
            foreach(Player player in players) {
                results.Add(RemovePlayerFromAllowedToAnnounceContraPlayers(player));
            }
            return results.All(c=> c==true);
        }

        internal void EnqueueSpectatorForApproval(Player player, Player spectator)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                PlayerState spectatorState = _Players.Single(p => p.Id == spectator.Id);
                playerState._SpectatorsWaitingForApproval.Enqueue(spectatorState);
            }
        }
        internal Player DequeueSpectator(Player player, bool allow)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                PlayerState spectator = playerState._SpectatorsWaitingForApproval.Dequeue();
                if (allow)
                {
                    playerState.AddSpectator(spectator);
                }
                return spectator;
            }
        }


        internal void TakeTrick()
        {
            lock (_Lock)
            {
                PlayerState winner = _Players.Single(p => p.Id == Trick.Winner.Id);
                winner.AddPoints(Trick.Points);
                _TrickCount++;
                // Special case Bettel: Check if winner of trick is leader / announced the game
                if ( ( _AnnouncedGame == GameType.Bettel || _AnnouncedGame == GameType.BettelBrett ) && winner == Leader)
                {
                    // Game Over
                    _TrickCount = _initial_number_of_cards_per_player;
                    //winner.AddPoints(-120);
                }

                // Special case for Tout-Games
                if ( ( _AnnouncedGame == GameType.GeierTout || _AnnouncedGame == GameType.WenzTout || _AnnouncedGame == GameType.FarbsoloTout) && winner != Leader)
                {
                    // Game Over
                    _TrickCount = _initial_number_of_cards_per_player;
                }
            }
        }

        public void FindTeams()
        {
            lock (_Lock)
            {
                //Set up the team combination
                _Group0.Clear();
                _Group1.Clear();
                for (int i = 0; i < 4; i++)
                {
                    if (_AnnouncedGame == GameType.Ramsch)
                    {
                        _Groups[i] = 0;
                        _Group0.Add(PlayingPlayers[i]);
                    }
                    else if (_AnnouncedGame == GameType.Sauspiel)
                    {
                        if (PlayingPlayers[i] == Leader)
                        {
                            _Groups[i] = 1;
                            _Group1.Add(PlayingPlayers[i]);
                        }
                        else
                        {
                            bool foundTheSau = false;
                            foreach (Card c in PlayingPlayers[i].GetHandCards())
                            {
                                if (c.Number == 11 && c.Color == Leader.AnnouncedColor)
                                {
                                    _Groups[i] = 1;
                                    _Group1.Add(PlayingPlayers[i]);
                                    foundTheSau = true;
                                    break;
                                }
                            }

                            if (!foundTheSau)
                            {
                                _Groups[i] = 0;
                                _Group0.Add(PlayingPlayers[i]);
                            }
                        }
                    }
                    else if (_AnnouncedGame == GameType.Hochzeit)
                    {
                        if (PlayingPlayers[i] == Leader || PlayingPlayers[i] == HusbandWife)
                        {
                            _Groups[i] = 1;
                            _Group1.Add(PlayingPlayers[i]);
                        }
                        else
                        {
                            _Groups[i] = 0;
                            _Group0.Add(PlayingPlayers[i]);
                        }
                    }
                    // Announcing player against the others
                    else
                    {
                        if (PlayingPlayers[i] == Leader)
                        {
                            _Groups[i] = 1;
                            _Group1.Add(PlayingPlayers[i]);
                        }
                        else
                        {
                            _Groups[i] = 0;
                            _Group0.Add(PlayingPlayers[i]);
                        }
                    }
                }
            }
        }

        ///<summary>Method <c>GetPartners</c> returns the partners of Player <c>player</c></summary>
        public List<Player> GetPartners(Player player) {
            List<Player> partners = new List<Player>();
            List<Player> temp = null;

            if (_Group0.Contains(player))
            {
                temp = _Group0;    
            }
            else if (_Group1.Contains(player))
            {
                temp = _Group1;
            }

            foreach (Player p in temp)
            {
                if (p != player)
                {
                    partners.Add(p);
                }
            }

            return partners;
        }
    }
}