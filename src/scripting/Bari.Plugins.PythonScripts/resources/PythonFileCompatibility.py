"""Script-local compatibility for Python 2 text files and XML byte writes."""
import builtins


class _TextFile:
    def __init__(self, stream):
        self._stream = stream

    def __getattr__(self, name):
        return getattr(self._stream, name)

    def __enter__(self):
        self._stream.__enter__()
        return self

    def __exit__(self, *args):
        return self._stream.__exit__(*args)

    def __iter__(self):
        return self

    def __next__(self):
        return next(self._stream)

    def write(self, value):
        if isinstance(value, (bytes, bytearray)):
            value = bytes(value).decode(self._stream.encoding, self._stream.errors)
        return self._stream.write(value)

    def writelines(self, values):
        for value in values:
            self.write(value)


def compat_open(file, mode='r', buffering=-1, encoding=None, errors=None,
                newline=None, closefd=True, opener=None):
    if 'b' in mode or encoding is not None or errors is not None:
        return builtins.open(file, mode, buffering, encoding, errors, newline, closefd, opener)
    # Round-trip undecodable legacy bytes rather than replacing them or failing
    # when an ASCII settings edit touches a non-UTF8 document. Explicit encodings
    # and binary mode retain their normal Python 3 behavior.
    stream = builtins.open(file, mode, buffering, 'utf-8', 'surrogateescape',
                           newline, closefd, opener)
    return _TextFile(stream)
