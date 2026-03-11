namespace StarLoom.Game;

public sealed class PlayerStateGame
{
    public bool IsPlayerAvailable()
    {
        return Svc.ClientState.IsLoggedIn;
    }
}
