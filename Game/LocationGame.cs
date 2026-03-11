namespace StarLoom.Game;

public sealed class LocationGame
{
    public bool IsInsideHouse()
    {
        return ReturnPointGame.IsInsideHouse();
    }

    public bool IsInsideInn()
    {
        return ReturnPointGame.IsInsideInn();
    }
}
