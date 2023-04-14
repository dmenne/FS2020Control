using System;

namespace FS2020Control
{
  public class FS2020Exception : Exception
  {
    public FS2020Exception()
    {
    }

    public FS2020Exception(string message)
        : base(message)
    {
    }

    public FS2020Exception(string message, Exception inner)
        : base(message, inner)
    {
    }
  }
}
