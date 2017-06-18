using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;
using HtmlAgilityPack;
using Iveonik.Stemmers;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace LSI
{
    class Program
    {
        private static string[] _bannedPrefixes = {"/", "<", "href", "alt", "disabled", "id", "src", "style", "title", "class"};
        private static char[] _punctuationMarks = 
        {
            '=', '.', ',', '?', '!', '(', ')', ':', 
            '-', '<', '>', '[', ']', '"', '\'', 
            '/', ';', '|', '&', '%', '#', '*', '+', 
            '0','1', '2', '3', '4', '@', '_',
            '5', '6', '7', '8', '9', '\n', '\r', '\t' 
        };

        public class DocumentWordsCountModel
        {
            public DocumentWordsCountModel()
            {
                WordsCount = new Dictionary<string, int>();
            }
            public Dictionary<string, int> WordsCount { get; set; }
            public string FileName { get; set; }
        }


        static void Main(string[] args)
        {
            var rnd = GetRandomMatrix(3, 5);
            var matrix = new[,] {{1.0, 1.0, 1.0}, {1.0, 1.0, 2.0}};
            NormalizeRows(matrix, 2, 3);
            var path = @"C:\Users\mareczek\Source\Repos\plsa\Sample documents";
            var allWords = new HashSet<string>();
            var stemmer = new EnglishStemmer();
            var documentsWordsCount = new List<DocumentWordsCountModel>();
            var documentsCount = 10;
            var htmlDocuments = new List<string>();
            foreach (var file in new DirectoryInfo(path).GetFiles())
            {
                using (var fs = file.OpenRead())
                {
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.Load(fs);
                    htmlDocuments.Add(file.Name);
                    var title = htmlDocument.DocumentNode.SelectSingleNode("//title");
                    var body = htmlDocument.DocumentNode.SelectSingleNode("//body");
                    if (body == null)
                    {
                        continue;
                    }
                    
                    var docCountModel = new DocumentWordsCountModel(){ FileName = file.Name };
                    

                    var bodyContent = body.InnerText;
                    var words = bodyContent.Split(' ');
                    if (title != null)
                    {
                        words = words.Union(title.InnerText.Split(' ', '\n', '\r', '\t')).ToArray();
                    }
                    
                    foreach (var word in words)
                    {
                        if (StartsWithPrefix(word))
                        {
                            continue;
                        }
                        var lowercase = HttpUtility.HtmlDecode(word).ToLower();
                        var trimmed = lowercase.Trim(_punctuationMarks);
                        if (_punctuationMarks.Any(x => trimmed.Contains(x)))
                        {
                            continue;
                        }
                        if (trimmed.Length < 2)
                        {
                            continue;
                        }
                        var stemmed = stemmer.Stem(trimmed);
                        if (!allWords.Contains(stemmed))
                        {
                            allWords.Add(stemmed);
                        }
                        if (docCountModel.WordsCount.ContainsKey(stemmed))
                        {
                            docCountModel.WordsCount[stemmed]++;
                        }
                        else
                        {
                            docCountModel.WordsCount.Add(stemmed, 1);
                        }
                    }

                    documentsWordsCount.Add(docCountModel);
                    if (documentsWordsCount.Count > documentsCount)
                    {
                        break;
                    }
                }
            }

            

            var allWordsCount = allWords.Count;
            var allDocumentsCount = documentsWordsCount.Count;
            var topicsCount = 2;

            var allWordsArray = allWords.ToArray();
            var wordsCountArray = documentsWordsCount.ToArray();
            var X = new double[allWordsCount, allDocumentsCount];
            for (var x = 0; x < allWordsArray.Length; x++)
            {
                for (var y = 0; y < wordsCountArray.Length; y++)
                {
                    if (documentsWordsCount[y].WordsCount.ContainsKey(allWordsArray[x]))
                    {
                        X[x, y] = documentsWordsCount[y].WordsCount[allWordsArray[x]];
                    }
                    else
                    {
                        X[x, y] = 0.0;
                    }
                }
            }

            plsa(TransposeRowsAndColumns(X), 2, 10);

            var P1 = GetRandomMatrix(allWordsCount, topicsCount);// new double[allWordsCount, topicsCount];
            var P2 = GetRandomMatrix(topicsCount, allDocumentsCount);// new double[topicsCount, allDocumentsCount];
            int sfgdgf = 10;
            while (--sfgdgf>0)
            {
                var avgDif = 0.0;
                for (var t = 0; t < allWordsCount; t++)
                {
                    for (var k = 0; k < topicsCount; k++)
                    {
                        var sum = 0.0;
                        for (var d = 0; d < allDocumentsCount; d++)
                        {
                            var nom = X[t, d];
                            var den = 0.0;
                            for (var nextK = 0; nextK < topicsCount; nextK++)
                            {
                                den += P1[t, nextK]*P2[nextK, d];
                            }
                            var frac = nom/den;
                            frac = frac*P2[k, d];
                            sum += frac;
                        }
                        if (P1[t, k] != 0.0)
                        {
                            var dif = Math.Abs(P1[t, k] * sum - P1[t, k]) / P1[t, k];
                            avgDif += dif;
                        }
                        P1[t, k] = P1[t, k]*sum;

                    }
                }
                NormalizeRows(P1, allWordsCount, topicsCount);

                for (var k = 0; k < topicsCount; k++)
                {
                    for (var d = 0; d < allDocumentsCount; d++)
                    {
                        var sum = 0.0;
                        for (var t = 0; t < allWordsCount; t++)
                        {
                            var nom = X[t, d];
                            var den = 0.0;
                            for (var nextK = 0; nextK < topicsCount; nextK++)
                            {
                                den += P1[t, nextK]*P2[nextK, d];
                            }
                            if (den != 0.0)
                            {
                                var frac = nom/den;
                                frac = frac*P1[t, k];
                                sum += frac;
                            }
                        }
                        
                        if (P2[k, d] != 0.0)
                        {
                            var dif = Math.Abs(P2[k, d] - P2[k, d] * sum) / P2[k, d];

                            avgDif += dif;
                        }
                        P2[k, d] = P2[k, d] * sum;
                       
                    }
                }
            //    avgDif = avgDif/(topicsCount*documentsCount + allWordsCount*topicsCount);
                NormalizeRows(P2, topicsCount, allDocumentsCount);
            }

            var topics = new List<string>[topicsCount];
            var topicsDocuments = new List<string>[topicsCount];
            for (var i = 0; i < topicsCount; i++)
            {
                topics[i] = new List<string>();
                topicsDocuments[i] = new List<string>();
            }
            for (var w = 0; w < allWordsCount; w++)
            {
                var max = 0.0;
                var max_ind = 0;
                for (var col = 0; col < topicsCount; col++)
                {
                    if (P1[w, col] > max)
                    {
                        max = P1[w, col];
                        max_ind = col;
                    }
                }
                topics[max_ind].Add(allWordsArray[w]);
            }

           
            for (var i = 0; i < allDocumentsCount; i++)
            {
                var max = 0.0;
                var max_ind = 0;
                for (var t = 0; t < topicsCount; t++)
                {
                    if (P2[t, i] > max)
                    {
                        max = P2[t, i];
                        max_ind = t;
                    }
                }
                topicsDocuments[max_ind].Add(htmlDocuments[i]);
            }

            var a = 2;

           // var topicsWithDocuments = new 
            //var allWordsArray = allWords.ToArray();
            //var wordsCountArray = documentsWordsCount.ToArray();

            //var matrix = Matrix<double>.Build.Dense(allWords.Count, documentsWordsCount.Count, 0.0);
            //for (var x = 0; x < allWordsArray.Length; x++)
            //{
            //    for (var y = 0; y < wordsCountArray.Length; y++)
            //    {
            //        if (documentsWordsCount[y].WordsCount.ContainsKey(allWordsArray[x]))
            //        {
            //            matrix[x, y] = documentsWordsCount[y].WordsCount[allWordsArray[x]];
            //        }
            //    }
            //}
            //var svd = matrix.Svd();

        }

        private static double[,] GetRandomMatrix(int rows, int columns)
        {
            var rnd = new Random();
            var result = new double[rows, columns];
            for (var row = 0; row < rows; row++)
            {
                var sum = 0.0;
                for (var col = 0; col < columns; col++)
                {
                    var next = rnd.NextDouble();
                    sum += next;
                    result[row,col] = next;
                }
                for (var col = 0; col < columns; col++)
                {
                    result[row, col] = result[row, col]/sum;
                }
            }
            return result;
        }

        private static void NormalizeRows(double[,] matrix)
        {
            NormalizeRows(matrix, matrix.GetLength(0), matrix.GetLength(1));
        }

        private static void NormalizeRows(double[,] matrix, int rows, int columns)
        {
            for (var row = 0; row < rows; row++)
            {
                var rowSum = 0.0;
                for (var col = 0; col < columns; col++)
                {
                    rowSum += matrix[row, col];
                }
                if (rowSum != 0.0)
                {
                    for (var col = 0; col < columns; col++)
                    {
                        matrix[row, col] = matrix[row, col]/rowSum;
                    }
                }
            }
        }

        private static bool StartsWithPrefix(string word)
        {
            return _bannedPrefixes.Any(word.StartsWith);
        }

        public static void plsa(double[,] documentWordMatrix, int numberOfTopics, int maxIterations = 100)
        {
            int numberOfDocuments = documentWordMatrix.GetLength(0); //rows N
            int numberOfWords = documentWordMatrix.GetLength(1); //cols M

            var documentTopicMatrix = GetRandomMatrix(numberOfDocuments, numberOfTopics); //P(z | d)
            var topicWordMatrix = GetRandomMatrix(numberOfTopics, numberOfWords); // P(w | z)
            var topicMatrix = new double[numberOfDocuments, numberOfWords, numberOfTopics]; //γ P(z | d, w)

            NormalizeRows(documentTopicMatrix);
            NormalizeRows(topicWordMatrix);

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                //stepE
                for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                {
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        double normalizeSum = 0.0;
                        double prob = 0.0;
                        for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                        {
                            prob = documentTopicMatrix[i_document_index, k_topic_index]*
                                   topicWordMatrix[k_topic_index, j_word_index]; //w pdfie malafiejskiego jest jeszcze lambda
                            assertNotZero(prob);
                            normalizeSum += prob;
                            topicMatrix[i_document_index, j_word_index, k_topic_index] = prob;
                        }
                        //normalizacja
                        for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                        {
                            topicMatrix[i_document_index, j_word_index, k_topic_index] /= normalizeSum;
                        }
                    }
                }
                //stepM
                for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                {
                    double normalizeSum = 0.0;
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        double s = 0.0;
                        for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value*topicMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                        assertNotZero(s);
                        topicWordMatrix[k_topic_index, j_word_index] = s;
                        normalizeSum += s;
                    }
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        topicWordMatrix[k_topic_index, j_word_index] /= normalizeSum;
                    }
                }

                for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                {
                    double normalizeSum = 0.0;
                    for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                    {
                        double s = 0.0;
                        for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value*topicMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                        assertNotZero(s);
                        documentTopicMatrix[i_document_index, k_topic_index] = s;
                        normalizeSum += s;
                    }
                    for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                    {
                        documentTopicMatrix[i_document_index, k_topic_index] /= normalizeSum;
                    }
                }
            }
            Console.WriteLine("Done");
        }

        public static void assertNotZero(double value)
        {
            if (value == 0.0)
            {
                Environment.Exit(1);
            }
        }

        public static T[,] TransposeRowsAndColumns<T>(T[,] arr)
        {
            int rowCount = arr.GetLength(0);
            int columnCount = arr.GetLength(1);
            T[,] transposed = new T[columnCount, rowCount];
            if (rowCount == columnCount)
            {
                transposed = (T[,])arr.Clone();
                for (int i = 1; i < rowCount; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        T temp = transposed[i, j];
                        transposed[i, j] = transposed[j, i];
                        transposed[j, i] = temp;
                    }
                }
            }
            else
            {
                for (int column = 0; column < columnCount; column++)
                {
                    for (int row = 0; row < rowCount; row++)
                    {
                        transposed[column, row] = arr[row, column];
                    }
                }
            }
            return transposed;
        }


    }
}
