namespace ToolBox.Pools
{
    public interface IPoolable
    {
        void OnPool();
        void OnDepool();
    }
}
