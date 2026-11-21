namespace BoxedWine.Lib;

/// <summary>The stable codes of <see cref="BwException"/>.</summary>
public static class BwErrorCodes
{
    /// <summary>Os-x86 is not installed, disabled, or out of the declared version range.</summary>
    public const string X86Missing = "x86-missing";

    /// <summary>The program cannot run yet (<see cref="BwException.Program"/> says why).</summary>
    public const string NotRunnable = "not-runnable";

    /// <summary>The program is larger than what Os-x86 copies into a prefix.</summary>
    public const string TooLarge = "too-large";
}

/// <summary>A refusal of BwLib, with a stable <see cref="Code"/> (<see cref="BwErrorCodes"/>).</summary>
public sealed class BwException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="code">The stable code.</param>
    /// <param name="message">An English description.</param>
    /// <param name="program">The program refused, when the refusal is about it.</param>
    public BwException(string code, string message, BwProgram? program = null)
        : base(message)
    {
        Code = code;
        Program = program;
    }

    /// <summary>The stable code (<see cref="BwErrorCodes"/>).</summary>
    public string Code { get; }

    /// <summary>The program refused, for <see cref="BwErrorCodes.NotRunnable"/>.</summary>
    public BwProgram? Program { get; }
}
