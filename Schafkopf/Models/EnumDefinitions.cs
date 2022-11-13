namespace Schafkopf.Models
{
    public enum Color { None, Schellen = 100, Herz = 200, Gras = 300, Eichel = 400 };
    public enum State { Idle, AnnounceHochzeit, Announce, AnnounceGameType, AnnounceGameColor, HochzeitExchangeCards, Playing };
    public enum GameType { Ramsch, Sauspiel, Hochzeit, Geier, Wenz, Bettel, Farbsolo, GeierTout, WenzTout, BettelOuvert, FarbsoloTout };
    public enum LastTrickButtonState { disabled, show, hide };
    public enum TakeTrickButtonState { hidden, won, lost };
    public enum Playing { Play, Pause, Undecided };
}