﻿using Lime.Protocol.Http.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lime.Protocol.Http.Processors
{
    public class GetEnvelopesHttpProcessor<T> : IHttpProcessor
        where T : Envelope
    {
        private readonly IEnvelopeStorage<T> _envelopeStorage;

        #region Constructor

        public GetEnvelopesHttpProcessor(IEnvelopeStorage<T> envelopeStorage, string path)
        {
            _envelopeStorage = envelopeStorage;

            Methods = new HashSet<string> { Constants.HTTP_METHOD_GET };
            Template = new UriTemplate(string.Format("/{0}", path));
        }

        #endregion

        #region IHttpProcessor Members

        public HashSet<string> Methods { get; private set; }

        public UriTemplate Template { get; private set; }

        public async Task<HttpResponse> ProcessAsync(HttpRequest request, UriTemplateMatch match, IEmulatedTransport transport, CancellationToken cancellationToken)
        {
            Identity owner;

            if (Identity.TryParse(request.User.Identity.Name, out owner))
            {
                var ids = await _envelopeStorage.GetEnvelopesAsync(owner).ConfigureAwait(false);
                if (ids.Length > 0)
                {
                    string body;

                    using (var writer = new StringWriter())
                    {
                        foreach (var id in ids)
                        {
                            await writer.WriteLineAsync(id.ToString()).ConfigureAwait(false);
                        }

                        body = writer.ToString();
                    }

                    var response = new HttpResponse(request.CorrelatorId, HttpStatusCode.OK, body: body);
                    response.Headers[HttpResponseHeader.ContentType] = Constants.TEXT_PLAIN_HEADER_VALUE;
                    return response;
                }
                else
                {
                    return new HttpResponse(request.CorrelatorId, HttpStatusCode.NoContent);
                }
            }
            else
            {
                return new HttpResponse(request.CorrelatorId, HttpStatusCode.BadRequest);
            }            
        }

        #endregion
    }
}
