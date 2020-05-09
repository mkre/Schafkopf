using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;

namespace Schafkopf.Models
{
    public interface Trick
    {
        public int Count
        {
            get;
        }
        public Card FirstCard
        {
            get;
        }
        public Player Winner
        {
            get;
        }
        public int Points
        {
            get;
        }
        public Task SendTrick(SchafkopfHub hub, Game game, List<String> connectionIds);
    }

    public class TrickState : Trick
    {
        public int _Count = 0;
        private readonly GameType GameType;
        private readonly Color TrumpColor;
        private Card[] Cards = new Card[4];
        private PlayerState[] Player = new PlayerState[4];
        private int WinnerIndex = 0;
        private int StartPlayer;

        public TrickState(GameType gameType, Color trump, int startPlayer)
        {
            GameType = gameType;
            TrumpColor = trump;
            StartPlayer = startPlayer;
        }

        //-------------------------------------------------
        // Card is added to the trick
        // in case that there are too many cards in one trick, an exception is thrown
        //-------------------------------------------------
        public void AddCard(Card card, PlayerState player)
        {
            if (_Count >= 4)
            {
                throw new Exception("There are too many Cards in the trick.");
            }
            Cards[_Count] = card;
            Player[_Count] = player;

            //Determine the winner of the Trick
            if (_Count > 0)
            {
                DetermineWinnerCard(card);
            }
            _Count++;
        }

        //-------------------------------------------------
        // FirstCard
        // WinnerCard
        // NewCard
        //-------------------------------------------------
        private void DetermineWinnerCard(Card newCard)
        {
            //Check which one is higher
            if (newCard.GetValue(GameType, TrumpColor, FirstCard) > Cards[WinnerIndex].GetValue(GameType, TrumpColor, FirstCard))
            {
                WinnerIndex = _Count;
            }
        }

        public Player Winner => Player[WinnerIndex];

        public Card FirstCard => Cards[0];

        public int Points => Cards[0].getPoints() + Cards[1].getPoints() + Cards[2].getPoints() + Cards[3].getPoints();

        public int Count => _Count;

        public async Task SendTrick(SchafkopfHub hub, Game game, List<String> connectionIds)
        {
            for (int i = 0; i < 4; i++)
            {
                Card[] permutedCards = new Card[4];
                for (int j = 0; j < 4; j++)
                {
                    permutedCards[j] = Cards[(j + i) % 4];
                }
                Player player = game.GameState.PlayingPlayers[(StartPlayer + i) % 4];
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    if (!connectionIds.Contains(connectionId))
                    {
                        continue;
                    }
                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceiveTrick",
                        permutedCards.Select(card => card == null ? "" : card.ToString())
                    );
                }
            }
        }
    }
}
