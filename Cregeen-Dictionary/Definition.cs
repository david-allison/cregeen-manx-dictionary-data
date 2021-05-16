using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Cregeen
{
    public class Definition
    {
        /// <summary>
        /// Html word
        /// </summary>
        private string originalWord;
        /// <summary>
        /// Html Extra
        /// </summary>
        private readonly string originalExtra;


        /// <summary>
        /// Decoded word
        /// </summary>
        public string Word { get; }
        public string Extra { get; }

        public string Entry => DecodeString(Extra);

        public List<Definition> Children { get; } = new List<Definition>();


        static HashSet<string> HeadWordsWithNoDefinitions = new HashSet<string>
        {
            "bastal", "booie", "by", "caagh", "co&#8209; prefix.", "cummey", "dyn", "erbee", "fenniu", "fy-",
            "gliee", "giyn",
            "jeear", "jioarey", "keddin", "laee", "lheh", "lhoys", "ner",
            "sheyn", "skew", "sprang-"
        };

        public Definition(string word, string extra)
        {
            this.originalWord = word;
            this.originalExtra = extra;
            Word = DecodeString(word);
            Extra = extra;
        }

        internal static Definition FromHtml(string arg1)
        {
            var split = arg1.Split(",").ToList();


            while (split[0].EndsWith("Times New Roman\"") && split.Count > 1)
            {
                split[0] = split[0] + split[1];
                split.RemoveAt(1);
            }

            // ". See" is a common element which does not contain a comma
            if (split[0].Contains(". See"))
            {
                var original = split[0];
                var index = original.IndexOf(". See");
                split[0] = original.Substring(0, index);
                if (split.Count == 1)
                {
                    split.Add("");
                }

                split[1] = original.Substring(index + ". ".Length) + split[1];
            }

            var word = split.First();

            // Certain headwords (cummey, jeear) appear both with and without definitions
            string definition = null;
            {
                try
                {
                    definition = string.Join(",", split.Skip(1));
                }
                catch
                {
                    if (!HeadWordsWithNoDefinitions.Contains(word.Trim()))
                    {
                        Console.WriteLine($"error: {word}");
                    }
                }
            }

            return new Definition(word, definition);
        }

        internal void AddChild(Definition node)
        {
            this.Children.Add(node);
            node.Parent = this;
        }

        public IEnumerable<Definition> AllChildren
        {
            get
            {
                yield return this;
                foreach (var child in Children.SelectMany(x => x.AllChildren))
                {
                    yield return child;
                }

            }
        }

        public IEnumerable<String> PossibleWords
        {
            get
            {
                // removing "sic" can cause duplicates
                return getPossibleWords().Select(x => x.Replace('‑', '-').Replace("’", "'").TrimAfter("[sic]", "(sic)", "[sic:", "(sic:").Trim()).Distinct();
            }
        }

        private int? depth = null;
        /// <summary>The nesting depth of an element is defined by the number of spaces before the word in the HTML</summary>
        public int Depth
        {
            get => depth ?? Word.Length - Word.TrimStart().Length;
            set => depth = value;
        }

        public Definition Parent { get; private set; }

        /// <summary>The HTML of the headword(s) for the entry</summary>
        public string Heading => this.originalWord;

        /// <summary>placed before such verbs where two are inserted, as, trog, the verb used alone; the one marked thus, trogg*, is the verb that is to be joined to  agh,  ee,  ey, &c.</summary>
        private static char VERB_HAS_SUFFIXES = '*';

        /// <summary>Reads the word and the definition to obtain the possible words which will link to this definition</summary>
        private IEnumerable<string> getPossibleWords()
        {
           List<string> words = Regex.Split(Word, "\\s+or\\s+").Select(x => x.Trim().Trim(VERB_HAS_SUFFIXES)).ToList();
            foreach (var w in words)
            {
                yield return w;
            }

            // Only apply suffixes to the words matked with an asterisk
            var wordsWithSuffixChanges = Word.Contains(VERB_HAS_SUFFIXES) ? 
                Regex.Split(Word, "\\s+or\\s+")
                .Where(x => x.Contains(VERB_HAS_SUFFIXES))
                .Select(x => x.Trim().Trim(VERB_HAS_SUFFIXES)).ToList() 
                : words;

            // TODO: How to handle "[change -agh to -ee], or  yn."
            var suffixes = Regex.Matches(Entry, "\\s(-|‑)\\w+").Select(x => x.Value.Trim(new char[] { '‑', '-', '\n', '\r', ' ' })).ToList();

            // Handle "[change -agh to -ee]"
            var suffixChanges = Regex.Matches(Entry, "\\[change [-|‑](.*)\\s+to\\s+[-|‑](.*)\\s*]");

            if (suffixChanges.Any())
            {
                foreach (Match changeInSuffix in suffixChanges)
                {
                    var suffixToFind = changeInSuffix.Groups[1].Value.Trim();

                    foreach (var word in wordsWithSuffixChanges)
                    {
                        if (word.EndsWith(suffixToFind))
                        {
                            yield return word.Substring(0, word.Length - suffixToFind.Length) + changeInSuffix.Groups[2].Value;
                        }
                    }

                    suffixes.Remove(suffixToFind);
                    suffixes.Remove(changeInSuffix.Groups[2].Value);
                }
            }

            foreach (var wordAsPrefix in wordsWithSuffixChanges)
            {
                foreach (string suffix in suffixes)
                {
                    yield return wordAsPrefix + suffix;
                }
            }
        }

        private static string DecodeString(string word)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(word);
            var wordAsString = doc.DocumentNode.InnerText;
            return HttpUtility.HtmlDecode(wordAsString).Trim('\r', '\n');
        }
    }


    public enum Abbreviations
    {
        Adjective, 
        Adverb, 
        AdjectiveDerivative, 
        AdjectivePlural,
        AdverbAndPronoun, 
        Article, 
        ArticlePlural, 
        ComparativeDegree, 
        Conjunction, 
        ConjunctionAndPronoun, 
        Diminutive, 
        Emphatically, 
        FeminineGender, 
        GalicGrGaelic, 
        HebrewAndBookOfHebrews, 
        TheSameAsAbove, 
        Interjection, 
        Literally, 
        Pronominal, 
        Plural, 
        PrepositionAndPronoun, 
        Preposition, 
        Pronoun, 
        ManksProverb, 
        Participle, 
        Substantive, 
        SubstantiveFeminine, 
        Singular, 
        SubstantiveMasculine,
        DoMasculineAndFeminine, 
        SubstantivePlural,
        SuperlativeDegree, 
        Synonymous,
        Verb,
        VerbImperative,
    }

}