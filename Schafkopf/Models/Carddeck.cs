using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Schafkopf.Models
{
    public class Carddeck
    {
        private readonly ReadOnlyCollection<Card> Cards;
        private readonly Random random = new Random();
        public readonly bool short_hand = true;

        public Carddeck()
        {
            List<Card> CardsList = new List<Card>();
            CardsList.Add(new Card(Color.Schellen, 9));
            CardsList.Add(new Card(Color.Schellen, 10));
            CardsList.Add(new Card(Color.Schellen, 2));
            CardsList.Add(new Card(Color.Schellen, 3));
            CardsList.Add(new Card(Color.Schellen, 4));
            CardsList.Add(new Card(Color.Schellen, 11));


            CardsList.Add(new Card(Color.Herz, 9));
            CardsList.Add(new Card(Color.Herz, 10));
            CardsList.Add(new Card(Color.Herz, 2));
            CardsList.Add(new Card(Color.Herz, 3));
            CardsList.Add(new Card(Color.Herz, 4));
            CardsList.Add(new Card(Color.Herz, 11));

            CardsList.Add(new Card(Color.Gras, 9));
            CardsList.Add(new Card(Color.Gras, 10));
            CardsList.Add(new Card(Color.Gras, 2));
            CardsList.Add(new Card(Color.Gras, 3));
            CardsList.Add(new Card(Color.Gras, 4));
            CardsList.Add(new Card(Color.Gras, 11));

            CardsList.Add(new Card(Color.Eichel, 9));
            CardsList.Add(new Card(Color.Eichel, 10));
            CardsList.Add(new Card(Color.Eichel, 2));
            CardsList.Add(new Card(Color.Eichel, 3));
            CardsList.Add(new Card(Color.Eichel, 4));
            CardsList.Add(new Card(Color.Eichel, 11));

            if (!short_hand)
            {
                CardsList.Add(new Card(Color.Schellen, 7));
                CardsList.Add(new Card(Color.Schellen, 8));

                CardsList.Add(new Card(Color.Herz, 7));
                CardsList.Add(new Card(Color.Herz, 8));

                CardsList.Add(new Card(Color.Gras, 7));
                CardsList.Add(new Card(Color.Gras, 8));

                CardsList.Add(new Card(Color.Gras, 7));
                CardsList.Add(new Card(Color.Gras, 8));

            }

            Cards = new ReadOnlyCollection<Card>(CardsList);
            /*
            Cards = new ReadOnlyCollection<Card>(new List<Card> {
                new Card(Color.Schellen, 7),
                new Card(Color.Schellen, 8),
                new Card(Color.Schellen, 9),
                new Card(Color.Schellen, 10),
                new Card(Color.Schellen, 2),
                new Card(Color.Schellen, 3),
                new Card(Color.Schellen, 4),
                new Card(Color.Schellen, 11),

                new Card(Color.Herz, 7),
                new Card(Color.Herz, 8),
                new Card(Color.Herz, 9),
                new Card(Color.Herz, 10),
                new Card(Color.Herz, 2),
                new Card(Color.Herz, 3),
                new Card(Color.Herz, 4),
                new Card(Color.Herz, 11),

                new Card(Color.Gras, 7),
                new Card(Color.Gras, 8),
                new Card(Color.Gras, 9),
                new Card(Color.Gras, 10),
                new Card(Color.Gras, 2),
                new Card(Color.Gras, 3),
                new Card(Color.Gras, 4),
                new Card(Color.Gras, 11),

                new Card(Color.Eichel, 7),
                new Card(Color.Eichel, 8),
                new Card(Color.Eichel, 9),
                new Card(Color.Eichel, 10),
                new Card(Color.Eichel, 2),
                new Card(Color.Eichel, 3),
                new Card(Color.Eichel, 4),
                new Card(Color.Eichel, 11),
        });
        */
        }

        public Card[] Shuffle()
        {
            Card[] shuffledCards;
            if (short_hand)
            {
                shuffledCards = new Card[24];
            } else
            {
                shuffledCards = new Card[32];
            }
            
            Cards.CopyTo(shuffledCards, 0);
            int n = Cards.Count;
            for (int i = 0; i < (n - 1); i++)
            {
                int r = i + random.Next(n - i);
                Card t = shuffledCards[r];
                shuffledCards[r] = shuffledCards[i];
                shuffledCards[i] = t;
            }
            return shuffledCards;
        }
    }
}