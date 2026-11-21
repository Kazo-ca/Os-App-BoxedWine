using System.Text;

namespace BoxedWine.Console;

/// <summary>
/// What a console program wrote, as lines of plain text for the window of BoxedWine (milestone 0:
/// a console in line mode, like the relay of <c>x86 run</c> in Os-x86). The bytes of the guest's
/// pseudo-terminal are decoded as UTF-8 across chunks; carriage return, backspace and tab move the
/// cursor of the current line; the escape sequences of the console of Wine are read and dropped,
/// except the cursor moves (<c>CSI n C</c>, how Wine writes blanks, <c>CSI n D</c>, <c>CSI n G</c>)
/// and the erase to the end of the line (<c>CSI K</c>). Nothing the guest writes is ever
/// interpreted as markup: the window shows the text as text.
/// </summary>
internal sealed class ConsoleTranscript
{
    /// <summary>The number of finished lines kept; older ones are dropped.</summary>
    public const int MaxLines = 2000;

    /// <summary>The longest line kept; what goes beyond is dropped.</summary>
    public const int MaxLineLength = 4096;

    private const int TabWidth = 8;
    private const int MaxSequenceLength = 32;
    private const char Escape = '\u001b';
    private const char Bell = '\u0007';

    private readonly Decoder _decoder = new UTF8Encoding(false, false).GetDecoder();
    private readonly LinkedList<string> _lines = new();
    private readonly StringBuilder _current = new();
    private readonly StringBuilder _sequence = new();
    private int _cursor;
    private EscapeState _state;

    private enum EscapeState
    {
        None,
        Escape,
        EscapeIntermediate,
        Csi,
        Osc,
        OscEscape,
    }

    /// <summary>The finished lines, oldest first.</summary>
    public IReadOnlyCollection<string> Lines => _lines;

    /// <summary>The line being written (no line feed yet).</summary>
    public string CurrentLine => _current.ToString();

    /// <summary>Every line, separated by line feeds, the current one last.</summary>
    public string Text
    {
        get
        {
            var text = new StringBuilder();
            foreach (string line in _lines)
            {
                text.Append(line).Append('\n');
            }

            return text.Append(_current).ToString();
        }
    }

    /// <summary>Adds bytes written by the program.</summary>
    /// <param name="bytes">A chunk of the output, cut anywhere (even inside a character).</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        char[] chars = new char[_decoder.GetCharCount(bytes, flush: false)];
        int count = _decoder.GetChars(bytes, chars, flush: false);
        foreach (char c in chars.AsSpan(0, count))
        {
            Feed(c);
        }
    }

    /// <summary>Adds a line BoxedWine writes itself (the exit code, an error), after what the program wrote.</summary>
    /// <param name="line">The text, without line feed; control characters are dropped.</param>
    public void AppendNote(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_current.Length > 0)
        {
            NewLine();
        }

        foreach (char c in line)
        {
            if (!char.IsControl(c))
            {
                Put(c);
            }
        }

        NewLine();
    }

    private void Feed(char c)
    {
        switch (_state)
        {
            case EscapeState.Escape:
                // ESC [ (CSI), ESC ] (OSC), ESC with intermediate bytes then a final one (ESC ( B),
                // or ESC and one final byte: all read, none written.
                _state = c switch
                {
                    '[' => EscapeState.Csi,
                    ']' => EscapeState.Osc,
                    >= ' ' and <= '/' => EscapeState.EscapeIntermediate,
                    _ => EscapeState.None,
                };
                _sequence.Clear();
                return;
            case EscapeState.EscapeIntermediate:
                _state = c is >= ' ' and <= '/' ? EscapeState.EscapeIntermediate : EscapeState.None;
                return;
            case EscapeState.Csi:
                if (c is >= '@' and <= '~')
                {
                    EndCsi(c);
                    _state = EscapeState.None;
                }
                else if (_sequence.Length < MaxSequenceLength)
                {
                    _sequence.Append(c);
                }

                return;
            case EscapeState.Osc:
                _state = c switch
                {
                    Bell => EscapeState.None,
                    Escape => EscapeState.OscEscape,
                    _ => EscapeState.Osc,
                };
                return;
            case EscapeState.OscEscape:
                _state = c == '\\' ? EscapeState.None : EscapeState.Osc;
                return;
        }

        FeedPlain(c);
    }

    private void FeedPlain(char c)
    {
        switch (c)
        {
            case Escape:
                _state = EscapeState.Escape;
                break;
            case '\n':
                NewLine();
                break;
            case '\r':
                _cursor = 0;
                break;
            case '\b':
                _cursor = Math.Max(0, _cursor - 1);
                break;
            case '\t':
                do
                {
                    Put(' ');
                }
                while (_cursor % TabWidth != 0 && _cursor < MaxLineLength);

                break;
            default:
                if (!char.IsControl(c))
                {
                    Put(c);
                }

                break;
        }
    }

    private void EndCsi(char final)
    {
        string parameters = _sequence.ToString();
        int count = int.TryParse(parameters, out int n) && n > 0 ? Math.Min(n, MaxLineLength) : 1;
        switch (final)
        {
            case 'C':
                // Moving forward over what is written keeps it; beyond the end, Wine means blanks.
                for (int i = 0; i < count && _cursor < MaxLineLength; i++)
                {
                    if (_cursor < _current.Length)
                    {
                        _cursor++;
                    }
                    else
                    {
                        Put(' ');
                    }
                }

                break;
            case 'D':
                _cursor = Math.Max(0, _cursor - count);
                break;
            case 'G':
                _cursor = Math.Min(count - 1, _current.Length);
                break;
            case 'K' when parameters is "" or "0":
                if (_cursor < _current.Length)
                {
                    _current.Length = _cursor;
                }

                break;
        }
    }

    private void Put(char c)
    {
        if (_cursor >= MaxLineLength)
        {
            return;
        }

        if (_cursor < _current.Length)
        {
            _current[_cursor] = c;
        }
        else
        {
            _current.Append(c);
        }

        _cursor++;
    }

    private void NewLine()
    {
        _lines.AddLast(_current.ToString());
        if (_lines.Count > MaxLines)
        {
            _lines.RemoveFirst();
        }

        _current.Clear();
        _cursor = 0;
    }
}
