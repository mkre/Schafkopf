namespace Schafkopf.Models
{
    public enum Color { None, Schellen = 100, Herz = 200, Gras = 300, Eichel = 400 };
    public enum State { Idle, AnnounceHochzeit, Announce, AnnounceGameType, AnnounceGameColor, HochzeitExchangeCards, Playing, Knock };
    // Note: Ranking in GameType has effect on decision whom of the players will be the leader / announce his/ger game (the higher the enum number/index, the higher the ranking)
    public enum GameType { Ramsch, Sauspiel, Hochzeit, Geier, Wenz, Bettel, Farbsolo, GeierTout, WenzTout, BettelBrett, FarbsoloTout };
    public enum LastTrickButtonState { disabled, show, hide };
    public enum TakeTrickButtonState { hidden, won, lost };
    public enum Playing { Play, Pause, Undecided };
}