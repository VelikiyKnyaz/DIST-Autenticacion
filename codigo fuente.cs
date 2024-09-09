using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System;
using UnityEngine.SocialPlatforms.Impl;

public class AuthHandler : MonoBehaviour
{
    [SerializeField]
    string URL = "https://sid-restapi.onrender.com";

    string Token;
    string Username;

    // Referencias a los elementos UI
    public TMP_InputField nuevoPuntajeInputField;
    public Button actualizarPuntajeButton;

    // Mensajes TextMeshProUGUI
    public TextMeshProUGUI mensajePuntaje;
    public TextMeshProUGUI mensajeBienvenida;
    public Transform leaderboardContainer;

    void Start()
    {
        Debug.Log("Escena iniciada. Esperando que el usuario inicie sesión o se registre.");
    }

    public void Registro()
    {
        // Método de registro que se llama desde la UI
        JsonData data = new JsonData
        {
            username = GameObject.Find("InputFieldUsername").GetComponent<TMP_InputField>().text,
            password = GameObject.Find("InputFieldPassword").GetComponent<TMP_InputField>().text,
            data = GameObject.Find("InputFieldUsername").GetComponent<TMP_InputField>().text
        };

        string postData = JsonUtility.ToJson(data);
        StartCoroutine(RegistroPost(postData));
    }

    IEnumerator RegistroPost(string postData)
    {
        UnityWebRequest request = UnityWebRequest.Post(URL + "/api/usuarios", postData, "application/json");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log("Error en el registro: " + request.error);
        }
        else
        {
            if (request.responseCode == 200)
            {
                Debug.Log("Registro exitoso.");
                // No hacemos Patch aquí, solo iniciamos sesión automáticamente
                StartCoroutine(LoginPost(postData));
            }
            else
            {
                Debug.Log("Error en el registro: " + request.downloadHandler.text);
            }
        }
    }

    public void Login()
    {
        // Método de inicio de sesión que se llama desde la UI
        JsonData data = new JsonData
        {
            username = GameObject.Find("InputFieldUsername").GetComponent<TMP_InputField>().text,
            password = GameObject.Find("InputFieldPassword").GetComponent<TMP_InputField>().text
        };

        string postData = JsonUtility.ToJson(data);
        StartCoroutine(LoginPost(postData));
    }

    IEnumerator LoginPost(string postData)
    {
        UnityWebRequest request = UnityWebRequest.Post(URL + "/api/auth/login", postData, "application/json");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log("Error en el login: " + request.error);
        }
        else
        {
            if (request.responseCode == 200)
            {
                AuthData authData = JsonUtility.FromJson<AuthData>(request.downloadHandler.text);
                PlayerPrefs.SetString("token", authData.token);
                PlayerPrefs.SetString("username", authData.usuario.username);

                Token = authData.token;
                Username = authData.usuario.username;

                // Mostrar el mensaje de bienvenida por 5 segundos
                mensajeBienvenida.text = $"Bienvenido, {Username}. Cada login te otorga 10 puntos.";
                StartCoroutine(MostrarMensajeBienvenida());

                // Obtenemos el perfil del usuario para ver su puntaje actual
                yield return StartCoroutine(GetPerfil());

                // Sumar 10 puntos al puntaje actual
                int nuevoPuntaje = authData.usuario.data.score + 10;
                StartCoroutine(PatchUsuario(Username, nuevoPuntaje));

                // Actualizar el mensaje de puntaje sin mostrar el valor
                mensajePuntaje.text = $"Puntaje para {Username}:";

                // Obtener y mostrar el leaderboard
                StartCoroutine(GetUsuarios());
            }
            else
            {
                Debug.Log("Error en el login: " + request.downloadHandler.text);
            }
        }
    }

    IEnumerator MostrarMensajeBienvenida()
    {
        // Mostrar el mensaje durante 5 segundos y luego ocultarlo
        yield return new WaitForSeconds(5);
        mensajeBienvenida.text = "";
    }

    IEnumerator GetPerfil()
    {
        string url = URL + "/api/usuarios/" + Username;
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("x-token", Token);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(request.error);
        }
        else
        {
            if (request.responseCode == 200)
            {
                AuthData data = JsonUtility.FromJson<AuthData>(request.downloadHandler.text);
                User user = data.usuario;
            }
            else
            {
                Debug.Log("El token no es válido o ha expirado.");
            }
        }
    }

    IEnumerator GetUsuarios()
    {
        string url = URL + "/api/usuarios";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("x-token", Token);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(request.error);
        }
        else
        {
            if (request.responseCode == 200)
            {
                UserList users = JsonUtility.FromJson<UserList>(request.downloadHandler.text);
                List<User> leaderboard = users.usuarios.OrderByDescending(u => u.data.score).ToList();

                // Mostrar el leaderboard en la UI
                MostrarLeaderboard(leaderboard);
            }
            else
            {
                Debug.Log("Error al obtener la lista de usuarios: " + request.downloadHandler.text);
            }
        }
    }

    void MostrarLeaderboard(List<User> leaderboard)
    {
        // Limpiar el contenedor antes de agregar nuevos elementos
        foreach (Transform child in leaderboardContainer)
        {
            Destroy(child.gameObject);
        }

        // Iterar sobre cada usuario y crear un nuevo texto en la UI para mostrar su puntaje
        foreach (User user in leaderboard)
        {
            GameObject userEntry = new GameObject("UserEntry");
            TextMeshProUGUI userText = userEntry.AddComponent<TextMeshProUGUI>();
            userText.text = $"{user.username} - {user.data.score}";

            // Asegurarse de que el nuevo GameObject esté correctamente alineado en el contenedor
            userEntry.transform.SetParent(leaderboardContainer);
            userText.fontSize = 24;
            userText.alignment = TextAlignmentOptions.Center;
        }
    }

    public void ActualizarPuntaje()
    {
        // Obtener el nuevo puntaje del input field y convertirlo a un número entero
        int nuevoPuntaje;
        if (int.TryParse(nuevoPuntajeInputField.text, out nuevoPuntaje))
        {
            // Actualizar el puntaje del usuario logueado
            StartCoroutine(PatchUsuario(Username, nuevoPuntaje));
        }
        else
        {
            Debug.Log("El puntaje ingresado no es válido.");
        }
    }

    IEnumerator PatchUsuario(string username, int score)
    {
        string url = URL + "/api/usuarios/";
        Debug.Log("PATCH URL: " + url);

        // Crear un objeto UserData con el nuevo puntaje
        UserData updatedData = new UserData(score, 0, 0, null);
        User updatedUser = new User { username = username, data = updatedData };
        string jsonData = JsonUtility.ToJson(updatedUser);

        Debug.Log("JSON Data: " + jsonData);

        UnityWebRequest request = UnityWebRequest.Put(url, jsonData);
        request.method = "PATCH";  // Especificar método PATCH
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-token", Token);

        yield return request.SendWebRequest();

        // Verificar el resultado de la solicitud
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log("Error en la actualización del puntaje: " + request.error);
        }
        else
        {
            Debug.Log("Response Code: " + request.responseCode);
            if (request.responseCode == 200)
            {
                Debug.Log("Puntaje actualizado correctamente.");
                mensajePuntaje.text = $"Nuevo puntaje para {Username}:";

                // Llamar al método GetUsuarios() para actualizar el leaderboard
                StartCoroutine(GetUsuarios());
            }
            else
            {
                Debug.Log("Error al actualizar el puntaje: " + request.downloadHandler.text);
            }
        }
    }
}

    [Serializable]
public class JsonData
{
    public string username;
    public string password;
    public string data;
}

[Serializable]
public class AuthData
{
    public User usuario;
    public string token;
}

[Serializable]
public class User
{
    public string username;
    public UserData data;
}

[Serializable]
public class UserData
{
    public int score;
    public int level;
    public int exp;
    public string color;

    public UserData(int score, int level, int exp, string color)
    {
        this.score = score;
        this.level = level;
        this.exp = exp;
        this.color = color;
    }
}
[Serializable]
public class UserList
{
    public User[] usuarios;  // Asegúrate de que coincida con la estructura de la respuesta de la API
}
