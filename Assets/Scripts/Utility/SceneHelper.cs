public enum SceneNames
{
    MainMenu,
    GamePlay
}

public static class SceneHelper
{
    public static string GetSceneName(SceneNames scene)
    {
        switch (scene)
        {
            case SceneNames.MainMenu:
                return "MainMenu";
            case SceneNames.GamePlay:
                return "GamePlay";
            default:
                return string.Empty;
        }
    }
}
