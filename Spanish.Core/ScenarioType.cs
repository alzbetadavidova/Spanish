namespace Spanish.Core;

public enum ScenarioType
{
    Card, // show a word, flip to see the translation, self-assess
    Fill, // show a word, type the translation
    Gender, // show a spanish noun, pick the article
    Plural, // show a spanish singular noun, type the plural
    Present, // conjugate a verb in present tense
    Preterite, // conjugate a verb in simple past tense
    Gerund, // type the gerund of a verb
    PairWithNoun // show an adjective and a noun, type the noun with the agreeing adjective form
}

public enum WordKind
{
    Noun,
    Verb,
    Adjective
}

public enum Direction
{
    Mixed,
    EnglishToSpanish,
    SpanishToEnglish
}

public enum SessionOrder
{
    LeastLearned,
    LeastRecentlyPracticed,
    Random
}

public enum Article
{
    El,
    La,
    Los,
    Las
}
