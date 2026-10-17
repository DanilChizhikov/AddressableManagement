namespace DTech.AddressableManagement.Internal.Loading
{
    internal readonly struct LoadPolicy
    {
        public RetryPolicy Retry { get; }
        public bool RebindShadersInEditor { get; }

        public LoadPolicy(RetryPolicy retry, bool rebindShadersInEditor)
        {
            Retry = retry;
            RebindShadersInEditor = rebindShadersInEditor;
        }
    }
}
