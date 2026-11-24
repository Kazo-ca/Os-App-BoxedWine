using System.Text;
using BoxedWine.Console;

namespace BoxedWine.Tests;

/// <summary>The console of the window: the bytes of the guest's pseudo-terminal as plain lines.</summary>
[Trait("Category", "unit")]
public sealed class ConsoleTranscriptTests
{
    private readonly ConsoleTranscript _transcript = new();

    [Fact]
    public void Lines_end_with_line_feed_and_carriage_return_goes_back_to_the_start()
    {
        Feed("hello\r\nworld\r\n12345\rab");

        Assert.Equal(["hello", "world"], _transcript.Lines);
        Assert.Equal("ab345", _transcript.CurrentLine);
        Assert.Equal("hello\nworld\nab345", _transcript.Text);
    }

    [Fact]
    public void A_character_cut_between_two_chunks_is_decoded_whole()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("été");
        _transcript.Append(bytes.AsSpan(0, 1));
        _transcript.Append(bytes.AsSpan(1));

        Assert.Equal("été", _transcript.CurrentLine);
    }

    [Fact]
    public void The_blanks_of_the_console_of_Wine_are_cursor_moves()
    {
        Feed("a\u001b[3Cb\u001b[Cc");

        Assert.Equal("a   b c", _transcript.CurrentLine);
    }

    [Fact]
    public void Moving_forward_over_text_keeps_it()
    {
        Feed("abcdef\r\u001b[2CX");

        Assert.Equal("abXdef", _transcript.CurrentLine);
    }

    [Fact]
    public void Cursor_back_column_and_erase_to_end_of_line()
    {
        Feed("abcdef\u001b[2DX\u001b[K");
        Assert.Equal("abcdX", _transcript.CurrentLine);

        Feed("\u001b[2GY");
        Assert.Equal("aYcdX", _transcript.CurrentLine);
    }

    [Fact]
    public void Colours_titles_and_other_sequences_are_dropped()
    {
        Feed("\u001b[1;31mred\u001b[0m \u001b]0;title\u0007ok \u001b]2;t\u001b\\done \u001b(Bx\u001b[?25l");

        Assert.Equal("red ok done x", _transcript.CurrentLine);
    }

    [Fact]
    public void A_title_that_never_ends_does_not_hide_what_follows()
    {
        Feed("\u001b]0;" + new string('t', 300) + "visible");

        Assert.EndsWith("visible", _transcript.CurrentLine, StringComparison.Ordinal);
        // The first 256 characters of the title are read as the title, the rest is written.
        Assert.True(_transcript.CurrentLine.Length < 300 - 200, _transcript.CurrentLine);
    }

    [Fact]
    public void Cancel_and_control_characters_end_a_sequence()
    {
        Feed("\u001b]2;t\u0018a\u001b[1\u001ab\u001b[3\nc\u001b[2\u001b[1Cd");

        Assert.Equal(["ab"], _transcript.Lines);
        Assert.Equal("c d", _transcript.CurrentLine);
    }

    [Fact]
    public void The_text_follows_every_change()
    {
        Feed("a");
        Assert.Equal("a", _transcript.Text);

        Feed("b\n");
        _transcript.AppendNote("end");

        Assert.Equal("ab\nend\n", _transcript.Text);
    }

    [Fact]
    public void Backspace_tab_and_control_characters()
    {
        Feed("ab\bc\tz\u0001\u0007");

        Assert.Equal("ac      z", _transcript.CurrentLine);
    }

    [Fact]
    public void Markup_from_the_guest_stays_text()
    {
        Feed("<script>alert(1)</script>");

        Assert.Equal("<script>alert(1)</script>", _transcript.CurrentLine);
    }

    [Fact]
    public void Old_lines_are_dropped_and_long_lines_are_cut()
    {
        for (int i = 0; i < ConsoleTranscript.MaxLines + 5; i++)
        {
            Feed($"{i}\n");
        }

        Feed(new string('x', ConsoleTranscript.MaxLineLength + 10));

        Assert.Equal(ConsoleTranscript.MaxLines, _transcript.Lines.Count);
        Assert.Equal("5", _transcript.Lines.First());
        Assert.Equal(ConsoleTranscript.MaxLineLength, _transcript.CurrentLine.Length);
    }

    [Fact]
    public void A_note_goes_on_its_own_line_without_control_characters()
    {
        Feed("partial");
        _transcript.AppendNote("exit\u001b 3");

        Assert.Equal(["partial", "exit 3"], _transcript.Lines);
        Assert.Equal(string.Empty, _transcript.CurrentLine);
    }

    [Fact]
    public void An_empty_chunk_changes_nothing()
    {
        _transcript.Append([]);

        Assert.Equal(string.Empty, _transcript.Text);
    }

    private void Feed(string text) => _transcript.Append(Encoding.UTF8.GetBytes(text));
}
