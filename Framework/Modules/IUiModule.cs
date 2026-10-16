namespace ArcanesGroupFrames.Framework.Modules
{
    // Future modules participate in a common lifecycle, without coupling to UI screens.
    public interface IUiModule
    {
        string Id { get; }
        void Initialize();
        void Shutdown();
    }
}
