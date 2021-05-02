namespace CSV.Serialization.Exceptions
{
    using System;

    /// <summary>
    /// Custom exeception to handle invalid CSV's for serialization.
    /// </summary>
    public class InvalidCsvFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidCsvFormatException"/> class.
        /// Invalid Csv Format Exception.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public InvalidCsvFormatException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidCsvFormatException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="ex">The execption object.</param>
        public InvalidCsvFormatException(string message, Exception ex)
            : base(message, ex)
        {
        }
    }
}
