namespace CSV.Serialization.Exceptions
{
    using System;

    /// <summary>
    /// A unique exception that is an extension of Exception to clearly identify
    /// when an unsupported file is parsed.
    /// </summary>
    public class UnsupportedCsvException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnsupportedCsvException"/> class.
        /// Invalid Csv Format Exception.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public UnsupportedCsvException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnsupportedCsvException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="ex">The execption object.</param>
        public UnsupportedCsvException(string message, Exception ex)
            : base(message, ex)
        {
        }
    }
}
