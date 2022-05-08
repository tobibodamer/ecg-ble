using System;

namespace EcgBLEApp.Models
{
    public sealed class InvalidEcgFileException : Exception
    {
        public InvalidEcgFileException(string message) : base(message)
        {
        }
    }
}