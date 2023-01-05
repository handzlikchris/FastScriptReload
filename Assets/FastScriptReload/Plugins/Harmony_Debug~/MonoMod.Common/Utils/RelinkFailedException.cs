using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    class RelinkFailedException : Exception {

        public const string DefaultMessage = "MonoMod failed relinking";

        public IMetadataTokenProvider MTP;
        public IMetadataTokenProvider Context;

        public RelinkFailedException(IMetadataTokenProvider mtp, IMetadataTokenProvider context = null)
            : this(_Format(DefaultMessage, mtp, context), mtp, context) {
        }

        public RelinkFailedException(string message,
            IMetadataTokenProvider mtp, IMetadataTokenProvider context = null)
            : base(message) {
            MTP = mtp;
            Context = context;
        }

        public RelinkFailedException(string message, Exception innerException,
            IMetadataTokenProvider mtp, IMetadataTokenProvider context = null)
            : base(message ?? _Format(DefaultMessage, mtp, context), innerException) {
            MTP = mtp;
            Context = context;
        }

        protected static string _Format(string message,
            IMetadataTokenProvider mtp, IMetadataTokenProvider context) {
            if (mtp == null && context == null)
                return message;

            StringBuilder builder = new StringBuilder(message);
            builder.Append(" ");

            if (mtp != null)
                builder.Append(mtp.ToString());

            if (context != null)
                builder.Append(" ");

            if (context != null)
                builder.Append("(context: ").Append(context.ToString()).Append(")");

            return builder.ToString();
        }

    }
}
