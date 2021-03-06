﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Bucket.ErrorCode
{
    public interface IErrorCodeStore
    {
        string StringGet(string code);
        Task<string> StringGetAsync(string code);
    }
}
