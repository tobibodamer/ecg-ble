using System;
using System.Collections.Generic;
using System.Text;

namespace EcgBLEApp
{
    public interface IExternalStorage
    {
        string GetPath();
    }
}
