using Xunit;
using Schafkopf.Models;
using System;


namespace Schafkopf.UnitTests
{
    public class SchafkopfModels
    {
        private TrickState trick;
        private PlayerState[] players;
        [Fact]
        public void Wenz()
        {
            players = new PlayerState[4];
            players[0] = new PlayerState("P1", "");
            players[1] = new PlayerState("P2", "");
            players[2] = new PlayerState("P3", "");
            players[3] = new PlayerState("P4", "");

            trick = new TrickState(GameType.Wenz, Color.None, 0);
            trick.AddCard(new Card(Color.Herz, 8), players[0]);
            trick.AddCard(new Card(Color.Herz, 3), players[1]);
            trick.AddCard(new Card(Color.Gras, 3), players[2]);
            trick.AddCard(new Card(Color.Herz, 11), players[3]);

            Assert.Equal("P4", trick.Winner.Name);
        }
    }
}
