namespace CSV.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using CSV.Serialization.Attributes;
    using CSV.Serialization.Exceptions;
    using Validation;

    /// <summary>
    /// Serialize and Deserialize Lists of any object type to CSV.
    /// </summary>
    /// <typeparam name="T">Generic type representation.</typeparam>
    public class CSVSerializer<T>
        where T : class, new()
    {
        private List<Tuple<int, PropertyInfo>> properties;
        private string[] columnsinfile;
        private bool usingcustomheader;
        private bool containscsvcolumnorderattribute = false;

        private List<string> keywordsList = null;

        private Dictionary<int, T> parsedData = new Dictionary<int, T>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CSVSerializer{T}"/> class.
        /// Csv Serializer
        /// Initialize by selected properties from the type to be de/serialized.
        /// </summary>
        public CSVSerializer()
        {
            var type = typeof(T);
            this.UseLineNumbers = false;

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance
                | BindingFlags.GetProperty | BindingFlags.SetProperty);

            if (!properties.Any())
            {
                throw new InvalidCsvFormatException($"There are no properties in {type.ToString()} to serialize.");
            }

            IQueryable<PropertyInfo> q = properties.AsQueryable();

            if (this.IgnoreReferenceTypesExceptString)
            {
                q = q.Where(a => a.PropertyType.IsValueType || a.PropertyType.Name == "String");
            }

            var r = from a in q
                    where a.GetCustomAttribute<CSVIgnoreAttribute>() == null
                    orderby a.Name
                    select a;

            this.containscsvcolumnorderattribute = r.Where(a => a.GetCustomAttribute<CSVColumnNameOrderAttribute>() != null).Any();
            if (this.containscsvcolumnorderattribute)
            {
                this.properties = r.Where(x => x.GetCustomAttribute<CSVColumnNameOrderAttribute>() != null)
                    .Select(x => new Tuple<int, PropertyInfo>(x.GetCustomAttribute<CSVColumnNameOrderAttribute>().Order, x))
                    .ToList();
            }
            else
            {
                this.properties = r.Select(x => new Tuple<int, PropertyInfo>(0, x)).ToList();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether when the data is seralized empty lines need to be considered part of the data.
        /// </summary>
        public bool IgnoreEmptyLines { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether.
        /// </summary>
        public bool IgnoreReferenceTypesExceptString { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to replace the value with
        /// when a new line is encountered during serialization.
        /// </summary>
        public string NewlineReplacement { get; set; } = ((char)0x254).ToString();

        /// <summary>
        /// Gets or sets a value indicating whether the replacement string to set.
        /// </summary>
        public string SeperatorReplacement { get; set; } = ((char)0x255).ToString();

        /// <summary>
        /// Gets or sets a value indicating the row number of the header.
        /// </summary>
        public string RowNumberColumnTitle { get; set; } = "RowNumber";

        /// <summary>
        /// Gets or sets a value indicating the Seperator to use when parsing the file.
        /// </summary>
        public char CSVSeparator { get; set; } = ',';

        /// <summary>
        /// Gets or sets a value indicating whether the value indicating of Eof Literal needs to be considered in serailization.
        /// </summary>
        public bool UseEofLiteral { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to use Line numbers in serialization.
        /// </summary>
        public bool UseLineNumbers { get; set; } = true;

        /// <summary>
        /// Gets or sets this to use it to override the headers lookup 
        /// and instead use custom headers. This helps best when 
        /// working with new attributes added.
        /// </summary>
        public string SetExpectedHeaders { get; set; }

        /// <summary>
        /// Gets or sets this to use when there are no headers for
        /// the file that needs to be deserialized.
        /// </summary>
        public List<string> Headers
        {
            get
            {
                return this.properties.OrderBy(r => r.Item1).Select(r => r.Item2.Name).ToList();
            }
        }

        /// <summary>
        /// Deserialize the given object to a list.
        /// </summary>
        /// <param name="stream">The stream to use for Deserialization of the object.</param>
        /// <param name="keywordslist">A list of keywords to use as markers for including as lines that need to be read.</param>
        /// <returns>A list of the object that was deserialized.</returns>
        public async Task<IList<T>> Deserialize(Stream stream, List<string> keywordslist = null)
        {
            string[] rows;

            if (this.CheckFileHeader(stream) == false)
            {
                throw new InvalidCsvFormatException("Invalid CSV found.");
            }

            var reader = new StreamReader(stream);
            {
                reader.DiscardBufferedData();
                reader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
                var content = reader.ReadToEnd();
                rows = content.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            }

            List<string> rowsList = rows.ToList(); // Remove the header;
            if (!this.usingcustomheader)
            {
                rowsList.RemoveAt(0);
            }

            this.keywordsList = keywordslist;
            IList<T> data = new List<T>();
            this.parsedData = new Dictionary<int, T>();

            List<string> rowsToParseList = null;
            if (this.keywordsList != null && this.keywordsList.Count > 0)
            {
                rowsToParseList = (from line in rowsList
                                   from keyword in this.keywordsList
                                   where line.ToUpper(CultureInfo.InvariantCulture)
                                   .Contains(keyword.ToUpper(CultureInfo.InvariantCulture))
                                   select line).Distinct().ToList();
            }
            else
            {
                rowsToParseList = rowsList;
            }

#if DEBUG
            Stopwatch timer;
            timer = new Stopwatch();
            timer.Start();
#endif

            if (Debugger.IsAttached)
            {
                for (int linecount = 0; linecount < rowsToParseList.Count; linecount++)
                {
                    this.ReadLine(linecount, rowsToParseList[linecount]);
                }
            }
            else
            {
                await Task.Run(() => Parallel.For(0, rowsToParseList.Count, i => this.ReadLine(i, rowsToParseList[i])));
            }

#if DEBUG
            timer.Stop();
            Debug.WriteLine(timer.Elapsed.TotalSeconds);
#endif

            data = this.parsedData
                .Where(r => r.Value != null)
                .OrderBy(r => r.Key)
                .Select(r => r.Value).ToList();

            return data;
        }

        /// <summary>
        /// A quick helper method to deserialize from a File object.
        /// </summary>
        /// <param name="file">A FileInfo object that represents a path to the file.</param>
        /// <returns>A List of parsed objects of Type T.</returns>
        public IList<T> Deserialize(FileInfo file)
        {
            Requires.NotNull(file, nameof(file));

            IList<T> returndatalist = null;
            if (file.Exists == false)
            {
                throw new ArgumentException("The file object passed in cannot be found.");
            }

            using (StreamReader reader = new StreamReader(file?.FullName))
            {
                returndatalist = this.Deserialize(reader.BaseStream, null).GetAwaiter().GetResult();
            }

            return returndatalist;
        }

        /// <summary>
        /// A quick helper method to deserialize from a File object.
        /// </summary>
        /// <param name="file">A FileInfo object that represents a path to the file.</param>
        /// <param name="keywordslist">A list of keywords to use as markers for including as lines that need to be read.</param>
        /// <returns>A List of parsed objects of Type T.</returns>
        public IList<T> Deserialize(FileInfo file, List<string> keywordslist)
        {
            Requires.NotNull(file, nameof(file));

            IList<T> returndatalist = null;
            if (file.Exists == false)
            {
                throw new ArgumentException("The file object passed in cannot be found.");
            }

            using (StreamReader reader = new StreamReader(file?.FullName))
            {
                returndatalist = this.Deserialize(reader.BaseStream, keywordslist).GetAwaiter().GetResult();
            }

            return returndatalist;
        }

        /// <summary>
        /// Serialize the data provided into the input file.
        /// </summary>
        /// <param name="file">File to save object data into.</param>
        /// <param name="data">The data to be serialized.</param>
        public void Serialize(string file, IList<T> data)
        {
            var values = new List<string>();

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            this.WriteToFile(file, this.GetTypeHeader());

            StringBuilder output = new StringBuilder();

            // The Header does not have a new line, so accomodating for that.
            this.WriteToFile(file, output.AppendLine().ToString());

            output.Clear();

            int fileflushcount = (int)(data.Count * 0.1); // Use 10% of
            if (fileflushcount == 0)
            {
                fileflushcount = data.Count;
            }

            int row = 1;
            int count = 1;
            foreach (var item in data)
            {
                if (count++ % fileflushcount == 0)
                {
                    this.WriteToFile(file, output.ToString());
                    output.Clear();
                }

                values.Clear();

                if (this.UseLineNumbers)
                {
                    values.Add(row.ToString());
                }

                List<Tuple<int, PropertyInfo>> propertieslist = null;
                if (this.containscsvcolumnorderattribute)
                {
                    propertieslist = this.properties.OrderBy(r => r.Item1).ToList();
                }
                else
                {
                    propertieslist = this.properties;
                }

                foreach (var p in propertieslist)
                {
                    var raw = p.Item2.GetValue(item);
                    var value = raw == null ? string.Empty :
                        raw.ToString()
                        .Replace(this.CSVSeparator.ToString(), this.SeperatorReplacement)
                        .Replace(Environment.NewLine, this.NewlineReplacement);

                    values.Add(value);
                }

                if (string.IsNullOrEmpty(values[0]))
                {
                    Debug.WriteLine("This should not happen");
                }

                output.AppendLine(string.Join(this.CSVSeparator.ToString(), values.ToArray()));
            }

            if (this.UseEofLiteral)
            {
                values.Clear();

                if (this.UseLineNumbers)
                {
                    values.Add(row++.ToString());
                }

                values.Add("EOF");

                output.AppendLine(string.Join(this.CSVSeparator.ToString(), values.ToArray()));
            }

            if (!string.IsNullOrEmpty(output.ToString()))
            {
                this.WriteToFile(file, output.ToString());
            }
        }

        /// <summary>
        /// Get Column Headers of the object to be deserialized dyanmically.
        /// </summary>
        /// <returns>The header string.</returns>
        public string GetTypeHeader()
        {
            var header = this.properties.Select(a => a.Item2.Name);

            if (this.UseLineNumbers)
            {
                header = new string[] { this.RowNumberColumnTitle }.Union(header);
            }

            string retheader = string.Empty;
            if (this.containscsvcolumnorderattribute == false)
            {
                var reportorderandproperties = this.properties
                .Select(r => r.Item2.Name).ToList();

                retheader = string.Join(this.CSVSeparator.ToString(), reportorderandproperties.ToArray());
            }
            else
            {
                var reportorderandproperties = this.properties
                .Select(r => new
                {
                    ReportAttribute = r.Item2.GetCustomAttribute<CSVColumnNameOrderAttribute>(),
                    Property = r,
                }).ToList();

                retheader = string.Join(this.CSVSeparator.ToString(),
                    reportorderandproperties.OrderBy(r => r.ReportAttribute.Order).Select(r => r.ReportAttribute.Title).ToArray());
            }

            return retheader;
        }

        /// <summary>
        /// This method allows the user to check ahead of time if the headers match using a File object.
        /// </summary>
        /// <param name="fileinfo">A file object to parse.</param>
        /// <returns>If the serialization was successfull.</returns>
        public bool CheckFileHeader(FileInfo fileinfo)
        {
            Requires.NotNull(fileinfo, nameof(fileinfo));

            bool retvalue = false;
            using (StreamReader reader = new StreamReader(fileinfo.FullName))
            {
                retvalue = this.CheckFileHeader(reader.BaseStream);
            }

            return retvalue;
        }

        /// <summary>
        /// This method allows the user to check ahead of time if the headers match using a Stream.
        /// </summary>
        /// <param name="stream">A Stream object used to parse.</param>
        /// <returns>If the serialization was successfull.</returns>
        public bool CheckFileHeader(Stream stream)
        {
            Requires.NotNull(stream, nameof(stream));

            var diffheaders = this.GetFileHeaderDiff(stream);
            return string.IsNullOrEmpty(diffheaders);
        }

        /// <summary>
        /// Gets the header for a file.
        /// </summary>
        /// <param name="fileInfo">The file to read.</param>
        /// <returns>Returns the header.</returns>
        public string GetFileHeaderDiff(FileInfo fileInfo)
        {
            Requires.NotNull(fileInfo, nameof(fileInfo));

            using (StreamReader reader = new StreamReader(fileInfo.FullName))
            {
                return this.GetFileHeaderDiff(reader.BaseStream);
            }
        }

        /// <summary>
        /// Gets the header for a file.
        /// </summary>
        /// <param name="stream">The Stream to read.</param>
        /// <returns>Returns the header.</returns>
        public string GetFileHeaderDiff(Stream stream)
        {
            Requires.NotNull(stream, nameof(stream));

            if (this.SetExpectedHeaders == null)
            {
                this.SetExpectedHeaders = this.GetTypeHeader();
            }

            string actualheaders = string.Empty;

            if (this.columnsinfile == null)
            {
                StreamReader sr = new StreamReader(stream);
                this.columnsinfile = sr.ReadLine().Split(this.CSVSeparator);
                this.columnsinfile.All(r =>
                {
                    r = r.Trim();
                    return true;
                });
            }

            int reportordercount = this.properties.Where(
                r => r.Item2.GetCustomAttribute<CSVColumnNameOrderAttribute>() != null).Count();

            if (reportordercount == 0)
            {
                this.columnsinfile = this.columnsinfile.Select(r => r.Replace(" ", string.Empty).ToUpperInvariant()).ToArray();
                actualheaders = string.Join(this.CSVSeparator.ToString(), this.columnsinfile.OrderBy(r => r).ToArray());
            }
            else
            {
                this.columnsinfile = this.columnsinfile.Select(r => r.Trim().ToUpperInvariant()).ToArray();
                actualheaders = string.Join(this.CSVSeparator.ToString(), this.columnsinfile);
            }

            if (this.SetExpectedHeaders.ToUpperInvariant() != actualheaders.ToUpperInvariant())
            {
                List<string> expectedheaderslist = this.SetExpectedHeaders.ToUpperInvariant().Split(new char[] { this.CSVSeparator }).ToList();
                List<string> actualheaderslist = actualheaders.ToUpperInvariant().Split(new char[] { this.CSVSeparator }).ToList();

                string diffheaders = string.Empty;
                var difflist = expectedheaderslist.Except(actualheaderslist);
                if (difflist.Any())
                {
                    diffheaders = string.Join(this.CSVSeparator.ToString(), difflist.ToArray());
                }

                return diffheaders;
            }

            return string.Empty;
        }

        private void WriteToFile(string path, string line)
        {
            Requires.NotNull(path, nameof(path));

            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.Write(line);
            }
        }

        private void ReadLine(int linenumber, string line)
        {
            if (this.IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            string[] parts = line.Split(this.CSVSeparator);

            var firstColumnIndex = this.UseLineNumbers ? 2 : 1;
            if (parts.Length == firstColumnIndex && parts[firstColumnIndex - 1] != null && parts[firstColumnIndex - 1] == "EOF")
            {
                return;
            }

            if (parts.Length < this.columnsinfile.Length)
            {
                Debug.WriteLine("Break here and investigate!.");
                //Debugger.Break();
            }

            T datum = new T();
            int start = this.UseLineNumbers ? 1 : 0;
            for (int i = start; i < this.columnsinfile.Length && i < parts.Length; i++)
            {
                string value = parts[i];
                if (i == this.columnsinfile.Length - 1 && parts.Length > this.columnsinfile.Length)
                {
                    List<string> partssubset = new List<string>();
                    for (int subsetcount = i; subsetcount < parts.Length; subsetcount++)
                    {
                        partssubset.Add(parts[subsetcount]);
                    }

                    value = string.Join(this.CSVSeparator.ToString(), partssubset.ToArray());
                }

                string column = this.columnsinfile[i];
                if (column.Contains(" "))
                {
                    column = column.Replace(" ", string.Empty);
                }

                if (column.Contains("#"))
                {
                    column = column.Replace("#", "Number");
                }

                // continue of deviant RowNumber column condition
                // this allows for the deserializer to implicitly ignore the RowNumber column
                if (column.Equals(RowNumberColumnTitle) &&
                    !this.properties.Any(a => a.Item2.Name.Equals(this.RowNumberColumnTitle)))
                {
                    continue;
                }

                PropertyInfo pi = null;
                if (this.containscsvcolumnorderattribute)
                {
                    foreach (Tuple<int, PropertyInfo> t in this.properties)
                    {
                        string comparisoncolumnname = t.Item2?.GetCustomAttribute<CSVColumnNameOrderAttribute>()?.Title;
                        comparisoncolumnname = comparisoncolumnname.Replace(" ", string.Empty).Replace("#", "Number");

                        if (comparisoncolumnname.Equals(column, StringComparison.InvariantCultureIgnoreCase))
                        {
                            pi = t.Item2;
                            break;
                        }
                    }
                }
                else
                {
                    pi = this.properties.Where(r =>
                        r.Item2.Name.ToString().Equals(column, StringComparison.InvariantCultureIgnoreCase))
                        .Select(r => r.Item2).FirstOrDefault();
                }

                // ignore property csv column, Property not found on targing type
                if (pi == null)
                {
                    continue;
                }

                TypeConverter converter = TypeDescriptor.GetConverter(pi.PropertyType);

                if (!string.IsNullOrEmpty(value.ToString()))
                {
                    value = value
                        .Replace(this.SeperatorReplacement, this.CSVSeparator.ToString())
                        .Replace(this.NewlineReplacement, Environment.NewLine).Trim();

                    var convertedvalue = converter.ConvertFrom(value);
                    pi.SetValue(datum, convertedvalue);
                }
                else
                {
                    pi.SetValue(datum, default(T));
                }
            }

            lock (this.parsedData)
            {
                this.parsedData.Add(linenumber, datum);
            }
        }
    }
}
