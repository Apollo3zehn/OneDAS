﻿using OneDas.Hdf.Explorer.Core;
using System.Threading.Tasks;

namespace OneDas.Hdf.Explorer.Web
{
    public interface IBroadcaster
    {
        Task SendState(HdfExplorerState hdfExplorerState);
        Task SendProgress(double percent, string message);
        Task SendByteCount(ulong byteCount);
    }
}
