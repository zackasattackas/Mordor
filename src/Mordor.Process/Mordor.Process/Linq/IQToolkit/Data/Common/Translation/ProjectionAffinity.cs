namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    public enum ProjectionAffinity
    {
        /// <summary>
        /// Prefer expression computation on the client
        /// </summary>
        Client,

        /// <summary>
        /// Prefer expression computation on the server
        /// </summary>
        Server
    }
}