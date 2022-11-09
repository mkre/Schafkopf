namespace Schafkopf.Models
{
    public enum Color { None, Schellen = 100, Herz = 200, Gras = 300, Eichel = 400 };
    public enum State { Idle, Announce, AnnounceGameType, AnnounceGameColor, Playing };
    public enum GameType { Ramsch, Sauspiel, Geier, Wenz, Bettel, Farbsolo, GeierTout, WenzTout, BettelOuvert, FarbsoloTout };
    public enum LastTrickButtonState { disabled, show, hide };
    public enum TakeTrickButtonState { hidden, won, lost };
    public enum Playing { Play, Pause, Undecided };
}