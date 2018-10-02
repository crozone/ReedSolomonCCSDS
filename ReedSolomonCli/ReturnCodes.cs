using System;
using System.Collections.Generic;
using System.Text;

namespace ReedSolomonCli
{
    public enum ReturnCodes
    {

        ExOk = 0, /* successful termination */
        ExBase = 64, /* base value for error messages */
        
        ExUsage = 64, /* command line usage error */
        ExDataErr = 65, /* data format error */
        ExNoInput = 66, /* cannot open input */
        ExNoUser = 67, /* addressee unknown */
        ExNoHost = 68, /* host name unknown */
        ExUnavailable = 69, /* service unavailable */
        ExSoftware = 70, /* internal software error */
        ExOsErr = 71, /* system error (e.g., can't fork) */
        ExOsFile = 72, /* critical OS file missing */
        ExCantCreate = 73, /* can't create (user) output file */
        ExIoErr = 74, /* input/output error */
        ExTempFail = 75, /* temp failure; user is invited to retry */
        ExProtocol = 76, /* remote error in protocol */
        ExNoPerm = 77, /* permission denied */
        ExConfig = 78, /* configuration error */
        
        ExMax = 78 /* maximum listed value */
    }
}
