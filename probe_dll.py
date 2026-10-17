from pathlib import Path
import ctypes as c
import argparse

parser = argparse.ArgumentParser(description='Initialize and shut down a supported x64 Iggy DLL.')
parser.add_argument('--dll', type=Path, help='Override automatic lookup in lib.')
args = parser.parse_args()
library_directory = Path(__file__).resolve().parent / 'lib'
path = args.dll or next((library_directory / name for name in
                       ('iggy_w64_final.dll', 'iggy_w64.dll')
                       if (library_directory / name).is_file()), None)
if path is None:
    parser.error('Cannot find lib/iggy_w64_final.dll or lib/iggy_w64.dll; use --dll PATH.')
kernel = c.WinDLL('kernel32', use_last_error=True)
kernel.SetErrorMode(3)
dll = c.CDLL(str(path.resolve()))
dll.IggyGetInfo.argtypes = [c.c_int]
dll.IggyGetInfo.restype = c.c_char_p
version = dll.IggyGetInfo(0).decode('utf-8')
print('DLL', path.resolve(), flush=True)
print('VERSION', version, flush=True)
dll.IggyInit.restype = None
dll.IggyShutdown.argtypes = []
dll.IggyShutdown.restype = None
print('INIT', flush=True)
if version.startswith('Iggy version 1.2.56\n'):
    # This build requires debug/name/malloc/free callbacks as a second argument.
    kernel.GetProcessHeap.restype = c.c_void_p
    kernel.HeapAlloc.argtypes = [c.c_void_p, c.c_uint, c.c_size_t]
    kernel.HeapAlloc.restype = c.c_void_p
    kernel.HeapFree.argtypes = [c.c_void_p, c.c_uint, c.c_void_p]
    heap = kernel.GetProcessHeap()
    pointer_callback = c.CFUNCTYPE(c.c_void_p, c.c_void_p)
    name_callback = c.CFUNCTYPE(c.c_void_p, c.c_void_p, c.c_size_t)
    free_callback = c.CFUNCTYPE(None, c.c_void_p)
    def debug_name(buffer, capacity):
        if capacity:
            c.memset(buffer, 0, 1)
        return buffer
    callbacks = (pointer_callback(lambda _: None), name_callback(debug_name),
                 pointer_callback(lambda size: kernel.HeapAlloc(heap, 0, size)),
                 free_callback(lambda address: kernel.HeapFree(heap, 0, address)))
    table = (c.c_void_p * 4)(*(c.cast(callback, c.c_void_p).value for callback in callbacks))
    dll.IggyInit.argtypes = [c.c_void_p, c.c_void_p]
    dll.IggyInit(None, table)
elif version.startswith('Iggy version 1.2.30\n'):
    dll.IggyInit.argtypes = [c.c_void_p]
    dll.IggyInit(None)
else:
    parser.error('Only the supplied Iggy 1.2.30 and 1.2.56 builds are supported.')
print('SHUTDOWN', flush=True)
dll.IggyShutdown()
print('OK', flush=True)
