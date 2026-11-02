namespace SelfControl.Core;

/// <summary>
/// Counts time the computer is in use and says when an hour has passed. A gap longer than 5 minutes between ticks
/// (sleep, PC off) starts the hour over, so waking the PC doesn't pop a suggestion straight away.
/// </summary>
public sealed class BreakClock
{
    public static readonly TimeSpan Every = TimeSpan.FromHours(1);
    static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(5);
    DateTime _last;
    TimeSpan _used;

    public BreakClock(DateTime now) => _last = now;

    /// <summary>Call regularly (e.g. every 30 s). True once per hour of use.</summary>
    public bool Tick(DateTime now)
    {
        var gap = now - _last;
        _last = now;
        if (gap < TimeSpan.Zero || gap > MaxGap) { _used = TimeSpan.Zero; return false; }
        _used += gap;
        if (_used < Every) return false;
        _used = TimeSpan.Zero;
        return true;
    }
}

public record ExerciseStep(string Title, string Text, int Seconds);

/// <summary>The stretches shown after "Alongamento" (one step at a time, with a countdown).</summary>
public static class Exercises
{
    public static readonly IReadOnlyList<ExerciseStep> Stretches = new ExerciseStep[]
    {
        new("Pescoço", "Incline a cabeça para a direita, aproximando a orelha do ombro. Respire devagar.", 15),
        new("Pescoço", "Agora para a esquerda.", 15),
        new("Ombros", "Gire os ombros para trás, em círculos grandes.", 15),
        new("Braços", "Entrelace os dedos e estique os braços acima da cabeça, palmas para cima.", 15),
        new("Peito", "Junte as mãos atrás das costas e abra o peito, levando os ombros para trás.", 15),
        new("Costas", "Sentado, gire o tronco para a direita segurando no encosto da cadeira.", 15),
        new("Costas", "Agora gire para a esquerda.", 15),
        new("Punhos", "Estique o braço direito e puxe os dedos para trás com a outra mão.", 15),
        new("Punhos", "Agora o braço esquerdo.", 15),
        new("Pernas", "Levante-se e caminhe um pouco, ou fique na ponta dos pés algumas vezes.", 30),
    };
}
