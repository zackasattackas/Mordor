using System;

namespace Mordor.Process.Internal
{
    internal interface ISafeDisposable : IDisposable
    {
        bool IsDisposed { get; }
    }
}