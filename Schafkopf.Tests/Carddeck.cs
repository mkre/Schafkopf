using Xunit;
using Schafkopf.Models;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Schafkopf.UnitTests
{
    public class CarddeckTest
    {
        [Fact]
        public void ShuffleTest()
        {
            Carddeck carddeck = new Carddeck();

            Assert.False(carddeck.Shuffle().SequenceEqual(carddeck.Shuffle()));
        }
    }
}
