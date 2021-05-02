namespace CSV.Serialization.Attributes
{
    using System;

    /// <summary>
    /// This is a custom attribute that is used on Object properties to
    /// denote that the property not to be serialized when Report data
    /// is serialized.
    /// </summary>
    public class CSVIgnoreAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CSVIgnoreAttribute"/> class.
        /// </summary>
        /// <param name="ignorereport">Flag to check if the attribute needs to be ignored in the report.</param>
        public CSVIgnoreAttribute(bool ignorereport = true)
        {
            this.IgnoreinReports = ignorereport;
        }

        /// <summary>
        /// Gets a value indicating whether gets flag if report attribute is ignored.
        /// </summary>
        public bool IgnoreinReports { get; private set; }
    }
}
