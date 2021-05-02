namespace CSV.Serialization.Attributes
{
    using System;

    /// <summary>
    /// This is a custom attribute that is used on Object properties to
    /// denote that the property needs to be serailized. In addition the
    /// actual name of the attribute (in case it's different from the
    /// attribute name) and the order value can be set to using this attribute.
    /// </summary>
    public class CSVColumnNameOrderAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CSVColumnNameOrderAttribute"/> class.
        /// </summary>
        /// <param name="title">The name of the attribute to be used.</param>
        /// <param name="order">The order in which the attribute needs to be serailized.</param>
        public CSVColumnNameOrderAttribute(string title, int order)
        {
            this.Order = order;
            this.Title = title;
        }

        /// <summary>
        /// Gets the order in which the attribute needs to be serailized.
        /// </summary>
        public int Order { get; private set; }

        /// <summary>
        /// Gets the name that needs to be used for the attribute to be serailized.
        /// </summary>
        public string Title { get; private set; }
    }
}
