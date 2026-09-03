namespace Template.Common.Models
{
    public class PagedResult<T>
    {
        public T? DataList { get; set; }
        public int TotalCount { get; set; }
    }
}

