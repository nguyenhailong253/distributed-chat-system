﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    public class Program
    {
        public static Proxy proxy = new Proxy();
        static void Main(string[] args)
        {
            proxy.StartListening();
        }
    }
}
