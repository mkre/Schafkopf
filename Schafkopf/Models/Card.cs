using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Schafkopf.Models
{
    public class Card
    {
        public readonly Color Color;
        public readonly int Number;

        public Card(Color color, int number)
        {
            Color = color;
            Number = number;
        }

        public int getPoints() {
            if (Number == 7 || Number == 8 || Number == 9) {
                return 0;
            }
            return Number;
        }

        public override string ToString()
        {
            return Color + "-" + Number;
        }

        public int GetValue(GameType gameType, Color trump, Card firstCard = null)
        {
            int value = 0;
            switch (gameType)
            {
                //Highest card looses
                case GameType.Ramsch:
                case GameType.Hochzeit:
                case GameType.Sauspiel:
                    {
                        //Determine value
                        if (Number == 2 || Number == 3)
                        {
                            value = 1000 * Number + (int)Color;
                        }
                        else if (Color == trump && Number == 4)
                        {
                            value = 500 + 5 * 9 + Number;
                        }
                        else if (Color == trump)
                        {
                            value = 500 + 5 * Number;
                        }
                        else if (firstCard != null && Color != firstCard.Color)
                        {
                            value = 0;
                        }
                        else if (Number == 4)
                        {
                            value = (int)Color + 5 * 9 + Number;
                        }
                        else
                        {
                            value = (int)Color + 5 * Number;
                        }
                    }
                    break;
                case GameType.Wenz:
                case GameType.WenzTout:
                    {
                        //Determine value
                        if (Number == 2)
                        {
                            value = 1000 * Number + (int)Color;
                        }
                        else if (firstCard != null && Color != firstCard.Color)
                        {
                            value = 0;
                        }
                        else if (Number == 4 || Number == 3)
                        {
                            value = (int)Color + 5 * 9 + Number;
                        }
                        else
                        {
                            value = (int)Color + 5 * Number;
                        }
                    }
                    break;
                case GameType.Farbsolo:
                case GameType.FarbsoloTout:
                    {
                        //Determine value
                        if (Number == 2 || Number == 3)
                        {
                            value = 1000 * Number + (int)Color;
                        }
                        else if (Color == trump && Number == 4)
                        {
                            value = 500 + 5 * 9 + Number;
                        }
                        else if (Color == trump)
                        {
                            value = 500 + 5 * Number;
                        }
                        else if (firstCard != null && Color != firstCard.Color)
                        {
                            value = 0;
                        }
                        else if (Number == 4)
                        {
                            value = (int)Color + 5 * 9 + Number;
                        }
                        else
                        {
                            value = (int)Color + 5 * Number;
                        }
                    }
                    break;
            }
            return value;
        }

        public bool IsTrump(GameType gameType, Color trump)
        {
            return GetValue(gameType, trump) > 500;
        }
    }
}
