using System;
using System.Collections.Specialized;

using MustardBlack.Handlers.Binding;
using MustardBlack.Routing;
using NSubstitute;
using Xunit;

namespace MustardBlack.Tests.Handlers.Binding.Binders.DateTimes
{
    public class WhenBindingMM_DD_YYYY : BindingSpecification
    {
        NameValueCollection post;
        DateTime target;

        protected override void Given()
        {
            base.Given();

            this.post = new NameValueCollection
	                        {
		                        {"datey", "02-01-2019 05:25 PM"},
	                        };

			this.Request.Form.Returns(this.post);
			this.Request.HttpMethod = HttpMethod.Post;
			this.Request.ContentType.Returns("application/x-www-form-urlencoded");
			this.Request.Url.Returns(new Url("http", "www.mydomain.com", 80, "/path", null));

        }

        protected override void When()
        {
			var binder = BinderCollection.FindBinderFor("datey", typeof(DateTime), this.Request, new RouteValues(), null);
			var bindingResult = binder.Bind("datey", typeof(bool?), this.Request, new RouteValues(), false, null);
            this.target = (DateTime)bindingResult.Object;
        }

        [Then]
        public void TheValueShouldBeCorrect()
        {
            this.target.ShouldEqual(new DateTime(2019, 02, 01, 17, 25, 0, DateTimeKind.Utc));
        }
    }
}
