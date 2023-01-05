#pragma warning disable IDE1006 // Naming Styles

using MonoMod.Utils;
using System;
using System.Runtime.InteropServices;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    unsafe class DetourNativeMonoPosixPlatform : IDetourNativePlatform {
        private readonly IDetourNativePlatform Inner;

        private readonly long _Pagesize;

        public DetourNativeMonoPosixPlatform(IDetourNativePlatform inner) {
            Inner = inner;

            _Pagesize = sysconf(SysconfName._SC_PAGESIZE, 0);
        }

        private static string GetLastError(string name) {
            int raw = _GetLastError();
            if (ToErrno(raw, out Errno errno) == 0) {
                return $"{name} returned {errno}";
            }
            return $"{name} returned 0x${raw:X8}";
        }

        private unsafe void SetMemPerms(IntPtr start, ulong len, MmapProts prot) {
            long pagesize = _Pagesize;
            long startPage = ((long) start) & ~(pagesize - 1);
            long endPage = ((long) start + (long) len + pagesize - 1) & ~(pagesize - 1);

            if (mprotect((IntPtr) startPage, (ulong) (endPage - startPage), prot) != 0)
                throw new Exception(GetLastError("mprotect"));
        }

        public void MakeWritable(IntPtr src, uint size) {
            // RWX because old versions of mono always use RWX.
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void MakeExecutable(IntPtr src, uint size) {
            // RWX because old versions of mono always use RWX.
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void MakeReadWriteExecutable(IntPtr src, uint size) {
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void FlushICache(IntPtr src, uint size) {
            // There is no cache flushing function in MPH.
            Inner.FlushICache(src, size);
        }

        public NativeDetourData Create(IntPtr from, IntPtr to, byte? type) {
            return Inner.Create(from, to, type);
        }

        public void Free(NativeDetourData detour) {
            Inner.Free(detour);
        }

        public void Apply(NativeDetourData detour) {
            Inner.Apply(detour);
        }

        public void Copy(IntPtr src, IntPtr dst, byte type) {
            Inner.Copy(src, dst, type);
        }

        public IntPtr MemAlloc(uint size) {
            return Inner.MemAlloc(size);
        }

        public void MemFree(IntPtr ptr) {
            Inner.MemFree(ptr);
        }

        // Good luck if your copy of Mono doesn't ship with MonoPosixHelper...
        [DllImport("MonoPosixHelper", SetLastError = true, EntryPoint = "Mono_Posix_Syscall_sysconf")]
        public static extern long sysconf(SysconfName name, Errno defaultError);

        [DllImport("MonoPosixHelper", SetLastError = true, EntryPoint = "Mono_Posix_Syscall_mprotect")]
        private static extern int mprotect(IntPtr start, ulong len, MmapProts prot);

        [DllImport("MonoPosixHelper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mono_Posix_Stdlib_GetLastError")]
        private static extern int _GetLastError();
        [DllImport("MonoPosixHelper", EntryPoint = "Mono_Posix_ToErrno")]
        private static extern int ToErrno(int value, out Errno rval);

        [Flags]
        private enum MmapProts : int {
            PROT_READ = 0x1,
            PROT_WRITE = 0x2,
            PROT_EXEC = 0x4,
            PROT_NONE = 0x0,
            PROT_GROWSDOWN = 0x01000000,
            PROT_GROWSUP = 0x02000000,
        }

        public enum SysconfName : int {
            _SC_ARG_MAX,
            _SC_CHILD_MAX,
            _SC_CLK_TCK,
            _SC_NGROUPS_MAX,
            _SC_OPEN_MAX,
            _SC_STREAM_MAX,
            _SC_TZNAME_MAX,
            _SC_JOB_CONTROL,
            _SC_SAVED_IDS,
            _SC_REALTIME_SIGNALS,
            _SC_PRIORITY_SCHEDULING,
            _SC_TIMERS,
            _SC_ASYNCHRONOUS_IO,
            _SC_PRIORITIZED_IO,
            _SC_SYNCHRONIZED_IO,
            _SC_FSYNC,
            _SC_MAPPED_FILES,
            _SC_MEMLOCK,
            _SC_MEMLOCK_RANGE,
            _SC_MEMORY_PROTECTION,
            _SC_MESSAGE_PASSING,
            _SC_SEMAPHORES,
            _SC_SHARED_MEMORY_OBJECTS,
            _SC_AIO_LISTIO_MAX,
            _SC_AIO_MAX,
            _SC_AIO_PRIO_DELTA_MAX,
            _SC_DELAYTIMER_MAX,
            _SC_MQ_OPEN_MAX,
            _SC_MQ_PRIO_MAX,
            _SC_VERSION,
            _SC_PAGESIZE,
            _SC_RTSIG_MAX,
            _SC_SEM_NSEMS_MAX,
            _SC_SEM_VALUE_MAX,
            _SC_SIGQUEUE_MAX,
            _SC_TIMER_MAX,
            /* Values for the argument to `sysconf'
                 corresponding to _POSIX2_* symbols.  */
            _SC_BC_BASE_MAX,
            _SC_BC_DIM_MAX,
            _SC_BC_SCALE_MAX,
            _SC_BC_STRING_MAX,
            _SC_COLL_WEIGHTS_MAX,
            _SC_EQUIV_CLASS_MAX,
            _SC_EXPR_NEST_MAX,
            _SC_LINE_MAX,
            _SC_RE_DUP_MAX,
            _SC_CHARCLASS_NAME_MAX,
            _SC_2_VERSION,
            _SC_2_C_BIND,
            _SC_2_C_DEV,
            _SC_2_FORT_DEV,
            _SC_2_FORT_RUN,
            _SC_2_SW_DEV,
            _SC_2_LOCALEDEF,
            _SC_PII,
            _SC_PII_XTI,
            _SC_PII_SOCKET,
            _SC_PII_INTERNET,
            _SC_PII_OSI,
            _SC_POLL,
            _SC_SELECT,
            _SC_UIO_MAXIOV,
            _SC_IOV_MAX = _SC_UIO_MAXIOV,
            _SC_PII_INTERNET_STREAM,
            _SC_PII_INTERNET_DGRAM,
            _SC_PII_OSI_COTS,
            _SC_PII_OSI_CLTS,
            _SC_PII_OSI_M,
            _SC_T_IOV_MAX,
            /* Values according to POSIX 1003.1c (POSIX threads).  */
            _SC_THREADS,
            _SC_THREAD_SAFE_FUNCTIONS,
            _SC_GETGR_R_SIZE_MAX,
            _SC_GETPW_R_SIZE_MAX,
            _SC_LOGIN_NAME_MAX,
            _SC_TTY_NAME_MAX,
            _SC_THREAD_DESTRUCTOR_ITERATIONS,
            _SC_THREAD_KEYS_MAX,
            _SC_THREAD_STACK_MIN,
            _SC_THREAD_THREADS_MAX,
            _SC_THREAD_ATTR_STACKADDR,
            _SC_THREAD_ATTR_STACKSIZE,
            _SC_THREAD_PRIORITY_SCHEDULING,
            _SC_THREAD_PRIO_INHERIT,
            _SC_THREAD_PRIO_PROTECT,
            _SC_THREAD_PROCESS_SHARED,
            _SC_NPROCESSORS_CONF,
            _SC_NPROCESSORS_ONLN,
            _SC_PHYS_PAGES,
            _SC_AVPHYS_PAGES,
            _SC_ATEXIT_MAX,
            _SC_PASS_MAX,
            _SC_XOPEN_VERSION,
            _SC_XOPEN_XCU_VERSION,
            _SC_XOPEN_UNIX,
            _SC_XOPEN_CRYPT,
            _SC_XOPEN_ENH_I18N,
            _SC_XOPEN_SHM,
            _SC_2_CHAR_TERM,
            _SC_2_C_VERSION,
            _SC_2_UPE,
            _SC_XOPEN_XPG2,
            _SC_XOPEN_XPG3,
            _SC_XOPEN_XPG4,
            _SC_CHAR_BIT,
            _SC_CHAR_MAX,
            _SC_CHAR_MIN,
            _SC_INT_MAX,
            _SC_INT_MIN,
            _SC_LONG_BIT,
            _SC_WORD_BIT,
            _SC_MB_LEN_MAX,
            _SC_NZERO,
            _SC_SSIZE_MAX,
            _SC_SCHAR_MAX,
            _SC_SCHAR_MIN,
            _SC_SHRT_MAX,
            _SC_SHRT_MIN,
            _SC_UCHAR_MAX,
            _SC_UINT_MAX,
            _SC_ULONG_MAX,
            _SC_USHRT_MAX,
            _SC_NL_ARGMAX,
            _SC_NL_LANGMAX,
            _SC_NL_MSGMAX,
            _SC_NL_NMAX,
            _SC_NL_SETMAX,
            _SC_NL_TEXTMAX,
            _SC_XBS5_ILP32_OFF32,
            _SC_XBS5_ILP32_OFFBIG,
            _SC_XBS5_LP64_OFF64,
            _SC_XBS5_LPBIG_OFFBIG,
            _SC_XOPEN_LEGACY,
            _SC_XOPEN_REALTIME,
            _SC_XOPEN_REALTIME_THREADS,
            _SC_ADVISORY_INFO,
            _SC_BARRIERS,
            _SC_BASE,
            _SC_C_LANG_SUPPORT,
            _SC_C_LANG_SUPPORT_R,
            _SC_CLOCK_SELECTION,
            _SC_CPUTIME,
            _SC_THREAD_CPUTIME,
            _SC_DEVICE_IO,
            _SC_DEVICE_SPECIFIC,
            _SC_DEVICE_SPECIFIC_R,
            _SC_FD_MGMT,
            _SC_FIFO,
            _SC_PIPE,
            _SC_FILE_ATTRIBUTES,
            _SC_FILE_LOCKING,
            _SC_FILE_SYSTEM,
            _SC_MONOTONIC_CLOCK,
            _SC_MULTI_PROCESS,
            _SC_SINGLE_PROCESS,
            _SC_NETWORKING,
            _SC_READER_WRITER_LOCKS,
            _SC_SPIN_LOCKS,
            _SC_REGEXP,
            _SC_REGEX_VERSION,
            _SC_SHELL,
            _SC_SIGNALS,
            _SC_SPAWN,
            _SC_SPORADIC_SERVER,
            _SC_THREAD_SPORADIC_SERVER,
            _SC_SYSTEM_DATABASE,
            _SC_SYSTEM_DATABASE_R,
            _SC_TIMEOUTS,
            _SC_TYPED_MEMORY_OBJECTS,
            _SC_USER_GROUPS,
            _SC_USER_GROUPS_R,
            _SC_2_PBS,
            _SC_2_PBS_ACCOUNTING,
            _SC_2_PBS_LOCATE,
            _SC_2_PBS_MESSAGE,
            _SC_2_PBS_TRACK,
            _SC_SYMLOOP_MAX,
            _SC_STREAMS,
            _SC_2_PBS_CHECKPOINT,
            _SC_V6_ILP32_OFF32,
            _SC_V6_ILP32_OFFBIG,
            _SC_V6_LP64_OFF64,
            _SC_V6_LPBIG_OFFBIG,
            _SC_HOST_NAME_MAX,
            _SC_TRACE,
            _SC_TRACE_EVENT_FILTER,
            _SC_TRACE_INHERIT,
            _SC_TRACE_LOG,
            _SC_LEVEL1_ICACHE_SIZE,
            _SC_LEVEL1_ICACHE_ASSOC,
            _SC_LEVEL1_ICACHE_LINESIZE,
            _SC_LEVEL1_DCACHE_SIZE,
            _SC_LEVEL1_DCACHE_ASSOC,
            _SC_LEVEL1_DCACHE_LINESIZE,
            _SC_LEVEL2_CACHE_SIZE,
            _SC_LEVEL2_CACHE_ASSOC,
            _SC_LEVEL2_CACHE_LINESIZE,
            _SC_LEVEL3_CACHE_SIZE,
            _SC_LEVEL3_CACHE_ASSOC,
            _SC_LEVEL3_CACHE_LINESIZE,
            _SC_LEVEL4_CACHE_SIZE,
            _SC_LEVEL4_CACHE_ASSOC,
            _SC_LEVEL4_CACHE_LINESIZE
        }

        public enum Errno : int {
            // errors & their values liberally copied from
            // FC2 /usr/include/asm/errno.h

            EPERM = 1, // Operation not permitted 
            ENOENT = 2, // No such file or directory 
            ESRCH = 3, // No such process 
            EINTR = 4, // Interrupted system call 
            EIO = 5, // I/O error 
            ENXIO = 6, // No such device or address 
            E2BIG = 7, // Arg list too long 
            ENOEXEC = 8, // Exec format error 
            EBADF = 9, // Bad file number 
            ECHILD = 10, // No child processes 
            EAGAIN = 11, // Try again 
            ENOMEM = 12, // Out of memory 
            EACCES = 13, // Permission denied 
            EFAULT = 14, // Bad address 
            ENOTBLK = 15, // Block device required 
            EBUSY = 16, // Device or resource busy 
            EEXIST = 17, // File exists 
            EXDEV = 18, // Cross-device link 
            ENODEV = 19, // No such device 
            ENOTDIR = 20, // Not a directory 
            EISDIR = 21, // Is a directory 
            EINVAL = 22, // Invalid argument 
            ENFILE = 23, // File table overflow 
            EMFILE = 24, // Too many open files 
            ENOTTY = 25, // Not a typewriter 
            ETXTBSY = 26, // Text file busy 
            EFBIG = 27, // File too large 
            ENOSPC = 28, // No space left on device 
            ESPIPE = 29, // Illegal seek 
            EROFS = 30, // Read-only file system 
            EMLINK = 31, // Too many links 
            EPIPE = 32, // Broken pipe 
            EDOM = 33, // Math argument out of domain of func 
            ERANGE = 34, // Math result not representable 
            EDEADLK = 35, // Resource deadlock would occur 
            ENAMETOOLONG = 36, // File name too long 
            ENOLCK = 37, // No record locks available 
            ENOSYS = 38, // Function not implemented 
            ENOTEMPTY = 39, // Directory not empty 
            ELOOP = 40, // Too many symbolic links encountered 
            EWOULDBLOCK = EAGAIN, // Operation would block 
            ENOMSG = 42, // No message of desired type 
            EIDRM = 43, // Identifier removed 
            ECHRNG = 44, // Channel number out of range 
            EL2NSYNC = 45, // Level 2 not synchronized 
            EL3HLT = 46, // Level 3 halted 
            EL3RST = 47, // Level 3 reset 
            ELNRNG = 48, // Link number out of range 
            EUNATCH = 49, // Protocol driver not attached 
            ENOCSI = 50, // No CSI structure available 
            EL2HLT = 51, // Level 2 halted 
            EBADE = 52, // Invalid exchange 
            EBADR = 53, // Invalid request descriptor 
            EXFULL = 54, // Exchange full 
            ENOANO = 55, // No anode 
            EBADRQC = 56, // Invalid request code 
            EBADSLT = 57, // Invalid slot 

            EDEADLOCK = EDEADLK,

            EBFONT = 59, // Bad font file format 
            ENOSTR = 60, // Device not a stream 
            ENODATA = 61, // No data available 
            ETIME = 62, // Timer expired 
            ENOSR = 63, // Out of streams resources 
            ENONET = 64, // Machine is not on the network 
            ENOPKG = 65, // Package not installed 
            EREMOTE = 66, // Object is remote 
            ENOLINK = 67, // Link has been severed 
            EADV = 68, // Advertise error 
            ESRMNT = 69, // Srmount error 
            ECOMM = 70, // Communication error on send 
            EPROTO = 71, // Protocol error 
            EMULTIHOP = 72, // Multihop attempted 
            EDOTDOT = 73, // RFS specific error 
            EBADMSG = 74, // Not a data message 
            EOVERFLOW = 75, // Value too large for defined data type 
            ENOTUNIQ = 76, // Name not unique on network 
            EBADFD = 77, // File descriptor in bad state 
            EREMCHG = 78, // Remote address changed 
            ELIBACC = 79, // Can not access a needed shared library 
            ELIBBAD = 80, // Accessing a corrupted shared library 
            ELIBSCN = 81, // .lib section in a.out corrupted 
            ELIBMAX = 82, // Attempting to link in too many shared libraries 
            ELIBEXEC = 83, // Cannot exec a shared library directly 
            EILSEQ = 84, // Illegal byte sequence 
            ERESTART = 85, // Interrupted system call should be restarted 
            ESTRPIPE = 86, // Streams pipe error 
            EUSERS = 87, // Too many users 
            ENOTSOCK = 88, // Socket operation on non-socket 
            EDESTADDRREQ = 89, // Destination address required 
            EMSGSIZE = 90, // Message too long 
            EPROTOTYPE = 91, // Protocol wrong type for socket 
            ENOPROTOOPT = 92, // Protocol not available 
            EPROTONOSUPPORT = 93, // Protocol not supported 
            ESOCKTNOSUPPORT = 94, // Socket type not supported 
            EOPNOTSUPP = 95, // Operation not supported on transport endpoint 
            EPFNOSUPPORT = 96, // Protocol family not supported 
            EAFNOSUPPORT = 97, // Address family not supported by protocol 
            EADDRINUSE = 98, // Address already in use 
            EADDRNOTAVAIL = 99, // Cannot assign requested address 
            ENETDOWN = 100, // Network is down 
            ENETUNREACH = 101, // Network is unreachable 
            ENETRESET = 102, // Network dropped connection because of reset 
            ECONNABORTED = 103, // Software caused connection abort 
            ECONNRESET = 104, // Connection reset by peer 
            ENOBUFS = 105, // No buffer space available 
            EISCONN = 106, // Transport endpoint is already connected 
            ENOTCONN = 107, // Transport endpoint is not connected 
            ESHUTDOWN = 108, // Cannot send after transport endpoint shutdown 
            ETOOMANYREFS = 109, // Too many references: cannot splice 
            ETIMEDOUT = 110, // Connection timed out 
            ECONNREFUSED = 111, // Connection refused 
            EHOSTDOWN = 112, // Host is down 
            EHOSTUNREACH = 113, // No route to host 
            EALREADY = 114, // Operation already in progress 
            EINPROGRESS = 115, // Operation now in progress 
            ESTALE = 116, // Stale NFS file handle 
            EUCLEAN = 117, // Structure needs cleaning 
            ENOTNAM = 118, // Not a XENIX named type file 
            ENAVAIL = 119, // No XENIX semaphores available 
            EISNAM = 120, // Is a named type file 
            EREMOTEIO = 121, // Remote I/O error 
            EDQUOT = 122, // Quota exceeded 

            ENOMEDIUM = 123, // No medium found 
            EMEDIUMTYPE = 124, // Wrong medium type 

            ECANCELED = 125,
            ENOKEY = 126,
            EKEYEXPIRED = 127,
            EKEYREVOKED = 128,
            EKEYREJECTED = 129,

            EOWNERDEAD = 130,
            ENOTRECOVERABLE = 131,

            // OS X-specific values: OS X value + 1000
            EPROCLIM = 1067, // Too many processes
            EBADRPC = 1072, // RPC struct is bad
            ERPCMISMATCH = 1073,    // RPC version wrong
            EPROGUNAVAIL = 1074,    // RPC prog. not avail
            EPROGMISMATCH = 1075,   // Program version wrong
            EPROCUNAVAIL = 1076,    // Bad procedure for program
            EFTYPE = 1079,  // Inappropriate file type or format
            EAUTH = 1080,   // Authentication error
            ENEEDAUTH = 1081,   // Need authenticator
            EPWROFF = 1082, // Device power is off
            EDEVERR = 1083, // Device error, e.g. paper out
            EBADEXEC = 1085,    // Bad executable
            EBADARCH = 1086,    // Bad CPU type in executable
            ESHLIBVERS = 1087,  // Shared library version mismatch
            EBADMACHO = 1088,   // Malformed Macho file
            ENOATTR = 1093, // Attribute not found
            ENOPOLICY = 1103,   // No such policy registered
        }
    }
}
