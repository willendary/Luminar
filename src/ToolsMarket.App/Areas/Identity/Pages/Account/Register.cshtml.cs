﻿#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using ToolsMarket.App.Data;
using ToolsMarket.App.ViewModels;
using ToolsMarket.Business.Interfaces;
using ToolsMarket.Business.Models;
using ToolsMarket.Business.Models.Enum;

namespace ToolsMarket.App.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUserModel> _signInManager;
        private readonly UserManager<ApplicationUserModel> _userManager;
        private readonly IUserStore<ApplicationUserModel> _userStore;
        private readonly IUserEmailStore<ApplicationUserModel> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IEnderecoRepository _enderecoRepository;
        private readonly IMapper _mapper;

        public RegisterModel(
            UserManager<ApplicationUserModel> userManager,
            IUserStore<ApplicationUserModel> userStore,
            SignInManager<ApplicationUserModel> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender, IEnderecoRepository enderecoRepository, IMapper mapper)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _enderecoRepository = enderecoRepository;
            _mapper = mapper;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [EmailAddress]
            [StringLength(100, ErrorMessage = "A {0} precisa ter entre {2} e {1} caracteres.", MinimumLength = 2)]
            [Display(Name = "E-mail")]
            public string Email { get; set; }

            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [StringLength(100, ErrorMessage = "A {0} precisa ter entre {2} e {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Senha")]
            public string Password { get; set; }

            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirmar Senha")]
            [Compare("Password", ErrorMessage = "As senhas não coincidem.")]
            public string ConfirmPassword { get; set; }

            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [StringLength(100, ErrorMessage = "A {0} precisa ter {1} caracteres.", MinimumLength = 3)]
            [DisplayName("Nome Completo")]
            public string Nome { get; set; }

            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [StringLength(11, ErrorMessage = "A {0} precisa ter {1} caracteres.", MinimumLength = 11)]
            [DisplayName("CPF")]
            public string Cpf { get; set; }

            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [DisplayName("Gênero")]
            public Genero Genero { get; set; }

            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [StringLength(11, ErrorMessage = "A {0} precisa ter {1} caracteres.", MinimumLength = 11)]
            [DisplayName("Telefone")]
            public string Telefone { get; set; }

            public TipoUsuario TipoUsuario { get; set; }

            // Relations
            [Required(ErrorMessage = "O campo {0} é obrigatório.")]
            [DisplayName("Endereço")]
            public EnderecoViewModel Endereco { get; set; }
            public Guid EnderecoId { get; set; }

            public IEnumerable<PedidoViewModel>? Pedido { get; set; }
            public Guid? PedidoId { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var endereco = new Endereco
                {
                    Cep = Input.Endereco.Cep,
                    Logradouro = Input.Endereco.Logradouro,
                    Numero = Input.Endereco.Numero,
                    Bairro = Input.Endereco.Bairro,
                    Cidade = Input.Endereco.Cidade,
                    Uf = Input.Endereco.Uf,
                };

                var user = CreateUser();
                user.Nome = Input.Nome;
                user.Cpf = Input.Cpf;
                user.Telefone = Input.Telefone;
                user.Genero = Input.Genero;
                user.UserName = Input.Email;
                user.EnderecoId = endereco.Id;

                endereco.Cliente = _mapper.Map<ApplicationUser>(user);
                endereco.ClienteId = new Guid(user.Id);

                await _enderecoRepository.Adicionar(endereco);

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirme seu e-mail",
                        $"Por favor, confirme sua seu e-mail <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>aqui</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }

        private ApplicationUserModel CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUserModel>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUserModel)}'. " +
                    $"Ensure that '{nameof(ApplicationUserModel)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUserModel> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUserModel>)_userStore;
        }
    }
}
