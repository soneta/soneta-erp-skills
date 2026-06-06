using NUnit.Framework;

// Testy Soneta bazują na TestBase/SessionState, które są single-threaded (stan sesji jest
// przypięty do wątku). Uruchamianie testów równolegle powoduje kolizję „Ponowne podłączenie
// stanu sesji". Wymuszamy wykonanie sekwencyjne (jeden worker, brak równoległości).
[assembly: LevelOfParallelism(1)]
[assembly: Parallelizable(ParallelScope.None)]
