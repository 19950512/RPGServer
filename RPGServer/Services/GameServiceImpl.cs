using System.Text.Json;
using Grpc.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPG.Protos;
using RPGServer.Data;

namespace RPGServer.Services;

public class GameServiceImpl : GameService.GameServiceBase
{
    private readonly PlayerRepository _repository;

    public GameServiceImpl(IConfiguration config)
    {
        var connString = config.GetConnectionString("Postgres") 
                         ?? throw new Exception("Connection string not found");
        _repository = new PlayerRepository(connString);
    }

    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var existing = await _repository.GetPlayerByEmailAsync(request.Email);
        if (existing != null)
        {
            return new RegisterResponse { Success = false, Message = "Email já registrado." };
        }

        bool ok = await _repository.RegisterPlayerAsync(request.Email, request.Password, request.JsonData);

        Console.WriteLine($"[REGISTER] {request.Email}");

        return new RegisterResponse
        {
            Success = ok,
            Message = ok ? "Conta criada com sucesso!" : "Erro ao criar conta"
        };
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var player = await _repository.GetPlayerByEmailAsync(request.Email);
        if (player == null)
        {
            return new LoginResponse { Success = false, Message = "Conta não encontrada" };
        }

        Console.WriteLine($"[LOGIN] {player.Email}");

        return new LoginResponse
        {
            Success = true,
            Message = "Login OK",
            Player = player
        };
    }

    public override async Task<SaveResponse> SavePlayer(PlayerData request, ServerCallContext context)
    {
        bool ok = await _repository.SavePlayerAsync(request);
        Console.WriteLine($"[SAVE] {request.Email}");

        return new SaveResponse
        {
            Success = ok,
            Message = ok ? "Progresso salvo!" : "Erro ao salvar"
        };
    }
}
