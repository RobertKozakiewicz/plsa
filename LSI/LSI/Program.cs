using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Management;
using System.Web.WebSockets;
using HtmlAgilityPack;
using Iveonik.Stemmers;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using MathNet.Numerics.Random;

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
            public string title2 { get; set; }
        }

        static List<DocumentWordsCountModel> documentsWordsCount = new List<DocumentWordsCountModel>();

        static void Main(string[] args)
        {
            var rnd = GetRandomMatrix(3, 5);
            var matrix = new[,] {{1.0, 1.0, 1.0}, {1.0, 1.0, 2.0}};
            NormalizeRows(matrix, 2, 3);
            var path = @"C:\Projects\IndexedDocuments";
            var stopwordspath = @"C:\Projects\IndexedDocuments\stopwords\stopwords_long.txt";
            var stopWords = new HashSet<String>();
            using (StreamReader sr = new StreamReader(stopwordspath))
            {
                var text = sr.ReadToEnd();
                var words = text.Split('\n').Select(w => w.Trim());
                foreach (var word in words)
                {
                    stopWords.Add(word);
                }
            }
            //var path = @"C:\Users\mareczek\PycharmProjects\cache_cam_html";
            var allWords = new HashSet<string>();
            var stemmer = new EnglishStemmer();
            var topicCount = 5;
            var documentsCount = 20;
            var htmlDocuments = new List<string>();
            foreach (var file in new DirectoryInfo(path).GetFiles())
            {
                using (var fs = file.OpenRead())
                {
                    Console.WriteLine(file.Name);
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.Load(fs);
                  //  htmlDocuments.Add(file.Name);
                    var title = htmlDocument.DocumentNode.SelectSingleNode("//title");
                    var body = htmlDocument.DocumentNode.SelectSingleNode("//body");
                    if (body == null)
                    {
                        continue;
                    }
                    
                    var docCountModel = new DocumentWordsCountModel(){ FileName = file.Name, title2 = title.InnerText };
                    

                    var bodyContent = body.InnerText;
                    var words = bodyContent.Split(' ');
                    if (title == null)
                    {
                        continue;
                    }
                    if (title != null)
                    {
                        htmlDocuments.Add(title.InnerText);
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
                        if (stopWords.Contains(word))
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
            plsa(TransposeRowsAndColumns(X), topicCount);
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

        public static void plsa(double[,] matrix, int topics, int maxIterations = 100)
        {
            int documents = matrix.GetLength(0); //rows N
            int words = matrix.GetLength(1); //cols M

            var docTop = Matrix<double>.Build.DenseOfArray(GetRandomMatrix(documents, topics)).NormalizeRows(1.0);
            var topWord = Matrix<double>.Build.DenseOfArray(GetRandomMatrix(topics, words)).NormalizeRows(1.0);
            var top = Matrix<double>.Build.DenseOfArray(GetRandomMatrix(1, topics)).Row(0).Normalize(1.0);
            var likelihoodMatrix = Matrix<double>.Build.Dense(documents, words);
            
            var docWordTop = Enumerable.Range(0, documents).Select(d => Matrix<double>.Build.Dense(words, topics)).ToList();
            var previous_L = 0.0;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // E
                // P(z|d,w)
                for (int d = 0; d < documents; d++)
                    for (int w = 0; w < words; w++)
                    {
                        for (int z = 0; z < topics; z++)
                            docWordTop[d][w, z] = top[z]*docTop[d, z]*topWord[z, w];
                        docWordTop[d].SetRow(w, docWordTop[d].Row(w).Normalize(1.0));
                    }
                // M
                // P(w|z)
                for (int z = 0; z < topics; z++)
                {
                    for (int w = 0; w < words; w++)
                        topWord[z, w] = Enumerable.Range(0, documents).Sum(d => matrix[d, w] * docWordTop[d][w, z]);
                    topWord.SetRow(z, topWord.Row(z).Normalize(1.0));
                }
                //topWord = topWord.NormalizeColumns(1.0);

                //P(d|z)
                for (int z = 0; z < topics; z++)
                {
                    for (int d = 0; d < documents; d++)
                        docTop[d, z] = Enumerable.Range(0, words).Sum(w => matrix[d, w]*docWordTop[d][w, z]);
                    docTop.SetColumn(z, docTop.Column(z).Normalize(1.0));
                }
                //docTop = docTop.NormalizeRows(1.0);

                //P(z)
                for (int z = 0; z < topics; z++)
                    top[z] = Enumerable.Range(0, documents)
                        .Sum(d => Enumerable.Range(0, words).Sum(w => matrix[d, w] * docWordTop[d][w, z]));
                top = top.Normalize(1.0);

                //likelihood
                for (int d = 0; d < documents; d++)
                    for (int w = 0; w < words; w++)
                        likelihoodMatrix[d, w] = Enumerable.Range(0, topics).Sum(z => top[z]*docTop[d, z]*topWord[z, w]);
                var likelihood = Enumerable.Range(0, documents)
                    .Sum(d => Enumerable.Range(0, words).Where(w => likelihoodMatrix[d, w] != 0).Sum(w => matrix[d, w]*Math.Log(likelihoodMatrix[d, w])));
                if (Math.Abs(likelihood - previous_L) < 1.0e-10) //mozna troche zwiekszyc ale trzeba uwazac bo pojawiają się NaN'y przez sumowanie / mnożenie małych liczb
                {
                    Console.WriteLine(iter);
                    break;
                }
                previous_L = likelihood;
            }
            var after = docTop.Multiply(Matrix<double>.Build.DenseOfDiagonalVector(top)).Multiply(topWord);
            printResults2(documents, Matrix<double>.Build.DenseOfArray(matrix), after);
            Console.WriteLine(docTop);
            Console.WriteLine(top);
            Console.WriteLine(words);
            Console.WriteLine(topWord);
            Console.WriteLine("Done3");
        }

        private static void printResults2(int numberOfDocuments, Matrix<double> org, Matrix<double> after)
        {
            for (int i = 0; i < numberOfDocuments; i++)
            {
                for (int j = i + 1; j < numberOfDocuments; j++)
                {
                    var orgCos = cosSimilarity(org.Row(i), org.Row(j));
                    var plsaCos = cosSimilarity(after.Row(i), after.Row(j));
                    if(plsaCos > 0.8)
                        Console.WriteLine("orginal: {0:0.0000} plsa: {1:0.0000} {2} - {3}", orgCos, plsaCos, documentsWordsCount[i].title2, documentsWordsCount[j].title2);
                }
            }
        }

       

        private static double cosSimilarity(Vector<double> a, Vector<double> b)
        {
            double dotProduct = Enumerable.Range(0, a.Count()).Sum(i => a[i] * b[i]);

            double firstVectorLength = Math.Sqrt(a.Sum(weight => Math.Pow(weight, 2)));
            double secondVectorLength = Math.Sqrt(b.Sum(weight => Math.Pow(weight, 2)));

            double lengths = (firstVectorLength * secondVectorLength);

            return (Math.Abs(lengths - 0.0) < double.Epsilon) ? 0 : dotProduct / lengths;
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
