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
    PairWithNoun, // adjective + noun: type the agreeing form; preposition: translate "from the park" to "del parque"
    NumberToText, // show a number, date or time in digits, type it in spanish words
    TextToNumber, // show a number, date or time in spanish words, type it in digits
    PresentEndings, // show a verb's root, type the endings of all its present forms
    PreteriteEndings // show a verb's root, type the endings of all its preterite forms
}

public enum WordKind
{
    Noun,
    Verb,
    Adjective,
    Preposition,
    Numeral
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
