﻿using System;
using System.IdentityModel.Tokens;
using System.Security.Claims;
using Thinktecture.IdentityModel.Tokens;
using Thinktecture.IdentityServer.Core.Connect.Models;
using Thinktecture.IdentityServer.Core.Connect.Services;
using Thinktecture.IdentityServer.Core.Services;
using Thinktecture.IdentityServer.Core.Extensions;

namespace Thinktecture.IdentityServer.Core.Connect
{
    public class AuthorizeResponseGenerator
    {
        private ITokenService _tokenService;
        private IAuthorizationCodeStore _authorizationCodes;
        private ITokenHandleStore _tokenHandles;
        private ICoreSettings _settings;

        public AuthorizeResponseGenerator(ITokenService tokenService, IAuthorizationCodeStore authorizationCodes, ITokenHandleStore tokenHandles, ICoreSettings settings)
        {
            _tokenService = tokenService;
            _authorizationCodes = authorizationCodes;
            _tokenHandles = tokenHandles;
            _settings = settings;
        }

        public AuthorizeResponse CreateCodeFlowResponse(ValidatedAuthorizeRequest request, ClaimsPrincipal user)
        {
            // create id and access token
            var idToken = _tokenService.CreateIdentityToken(request, user);
            var accessToken = _tokenService.CreateAccessToken(request, user);

            var code = new AuthorizationCode
            {
                ClientId = request.ClientId,
                IsOpenId = true,
                RequestedScopes = request.RequestedScopes.ToSpaceSeparatedString(),

                CreationTime = DateTime.UtcNow,
                RedirectUri = request.RedirectUri,

                IdentityToken = idToken,
                AccessToken = accessToken
            };

            // store id token and access token and return authorization code
            var id = Guid.NewGuid().ToString("N");
            _authorizationCodes.Store(id, code);

            return new AuthorizeResponse
            {
                RedirectUri = request.RedirectUri,
                Code = id,
                State = request.State
            };
        }

        public AuthorizeResponse CreateImplicitFlowResponse(ValidatedAuthorizeRequest request, ClaimsPrincipal user)
        {
            string jwt = null;
            if (request.IsOpenIdRequest)
            {
                var idToken = _tokenService.CreateIdentityToken(request, user);

                SigningCredentials credentials;
                if (request.Client.IdentityTokenSigningKeyType == SigningKeyTypes.ClientSecret)
                {
                    credentials = new HmacSigningCredentials(request.Client.ClientSecret);
                }
                else
                {
                    credentials = new X509SigningCredentials(_settings.GetSigningCertificate());
                }

                jwt = _tokenService.CreateJsonWebToken(idToken, credentials);
            }

            string accessTokenValue = null;
            int accessTokenLifetime = 0;
            if (request.IsResourceRequest)
            {
                var accessToken = _tokenService.CreateAccessToken(request, user);
                accessTokenLifetime = accessToken.Lifetime;

                if (request.Client.AccessTokenType == AccessTokenType.JWT)
                {
                    accessTokenValue = _tokenService.CreateJsonWebToken(
                        accessToken, 
                        new X509SigningCredentials(_settings.GetSigningCertificate()));
                }
                else
                {
                    accessTokenValue = Guid.NewGuid().ToString("N");
                    _tokenHandles.Store(accessTokenValue, accessToken);
                }
            }

            return new AuthorizeResponse
            {
                RedirectUri = request.RedirectUri,
                AccessToken = accessTokenValue,
                AccessTokenLifetime = accessTokenLifetime,
                IdentityToken = jwt,
                State = request.State,
                Scope = request.ValidatedScopes.GrantedScopes.ToSpaceSeparatedString()
            };
        }
    }
}
