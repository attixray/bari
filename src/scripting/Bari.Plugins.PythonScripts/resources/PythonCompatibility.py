"""Translate legacy build scripts in memory; never rewrite suite sources."""
import re
import sys
import types
from io import StringIO

# Grammar caches must not require write access to the Bari installation.
from lib2to3.pgen2 import driver
_load_grammar = driver.load_grammar
def _read_grammar(gt, gp=None, save=True, force=False, logger=None):
    return _load_grammar(gt, gp, save=False, force=force, logger=logger)
driver.load_grammar = _read_grammar
try:
    from lib2to3 import refactor
    from lib2to3.fixes.fix_dict import FixDict
    from lib2to3.fixes.fix_unicode import FixUnicode
    from lib2to3.fixes.fix_xrange import FixXrange
finally:
    driver.load_grammar = _load_grammar
from lib2to3.pgen2 import token, parse, tokenize


class FixBariDict(FixDict):
    # Keep Python 3 keys/items/values views unchanged. Only legacy methods
    # need translating, including iterator semantics outside a for loop.
    def transform(self, node, results):
        if not results['method'][0].value.startswith(('iter', 'view')):
            return None
        return super(FixBariDict, self).transform(node, results)


class FixBariUnicode(FixUnicode):
    def transform(self, node, results):
        if node.type == token.STRING and not node.value.startswith(('u', 'U')):
            return None
        return super(FixBariUnicode, self).transform(node, results)


class FixBariXrange(FixXrange):
    def transform(self, node, results):
        if results['name'].value != 'xrange':
            return None
        return super(FixBariXrange, self).transform(node, results)


for _name, _fixer in (('dict', FixBariDict), ('unicode', FixBariUnicode), ('xrange', FixBariXrange)):
    _module = types.ModuleType('fix_bari_' + _name)
    setattr(_module, _fixer.__name__, _fixer)
    sys.modules[_module.__name__] = _module


def _normalize_escapes(literal):
    prefix = re.match(r'(?i)^([rub]*)', literal).group(1).lower()
    if 'r' in prefix:
        return literal
    result = []
    index = 0
    lengths = {'x': 2, 'u': 4, 'U': 8}
    while index < len(literal):
        char = literal[index]
        if char == '\\' and index + 1 < len(literal):
            escape = literal[index + 1]
            size = lengths.get(escape)
            if size is not None:
                digits = literal[index + 2:index + 2 + size]
                if len(digits) != size or any(c not in '0123456789abcdefABCDEF' for c in digits):
                    # Python 2 byte strings allowed e.g. '\x' in Windows paths.
                    # Do not relax malformed explicitly-unicode literals.
                    if 'u' not in prefix:
                        result.append('\\')
            result.append(literal[index:index + 2])
            index += 2
        else:
            result.append(char)
            index += 1
    return ''.join(result)


class _RefactoringTool(refactor.RefactoringTool):
    def refactor_tree(self, tree, name):
        for leaf in tree.leaves():
            if leaf.type == token.STRING:
                leaf.value = _normalize_escapes(leaf.value)
        return super(_RefactoringTool, self).refactor_tree(tree, name)


_fixers = ['lib2to3.fixes.fix_' + name for name in (
    'print', 'except', 'raise', 'numliterals', 'has_key',
    'exec', 'long', 'imports', 'imports2')]
_fixers.extend(('fix_bari_dict', 'fix_bari_unicode', 'fix_bari_xrange'))
_python3 = _RefactoringTool(_fixers, {'print_function': True})
_python2 = _RefactoringTool(_fixers)


def prepare(source):
    source = source.replace('\r\n', '\n').replace('\r', '\n')
    if not source.endswith('\n'):
        source += '\n'
    # A redirected print is also a syntactically valid Python 3 shift expression.
    # Inspect tokens so strings/comments cannot accidentally select legacy mode.
    legacy_print = False
    try:
        tokens = [t for t in tokenize.generate_tokens(StringIO(source).readline)
                  if t[0] not in (tokenize.COMMENT, tokenize.NL)]
        for index, item in enumerate(tokens[:-1]):
            if item[0] == token.NAME and item[1] == 'print':
                previous = tokens[index - 1] if index else None
                statement_start = previous is None or previous[0] in (token.NEWLINE, token.INDENT, token.DEDENT) or previous[1] in (':', ';')
                if statement_start and (tokens[index + 1][1] == '>>' or tokens[index + 1][0] == token.NEWLINE):
                    legacy_print = True
    except (tokenize.TokenError, IndentationError):
        return source
    # Prefer function-call semantics for already-valid Python 3 print calls.
    order = (_python2, _python3) if legacy_print else (_python3, _python2)
    for tool in order:
        try:
            return str(tool.refactor_string(source, '<bari script>'))
        except (parse.ParseError, tokenize.TokenError, IndentationError):
            pass
    # Let IronPython report a normal syntax error with the actual script name.
    return source
