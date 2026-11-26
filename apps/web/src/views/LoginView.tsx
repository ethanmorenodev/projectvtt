import { useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';

/** Registro e inicio de sesión. */
export function LoginView() {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mode === 'login') {
        await login(userName, password);
      } else {
        await register(userName, password);
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Error inesperado.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="panel auth-panel">
      <h2>{mode === 'login' ? 'Iniciar sesión' : 'Crear cuenta'}</h2>
      <form onSubmit={submit}>
        <label>
          Nombre de usuario
          <input
            value={userName}
            onChange={(event) => setUserName(event.target.value)}
            autoComplete="username"
            required
            minLength={3}
            maxLength={32}
          />
        </label>
        <label>
          Contraseña
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            required
            minLength={8}
            maxLength={128}
          />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" disabled={busy}>
          {busy ? 'Esperando…' : mode === 'login' ? 'Entrar' : 'Registrarme'}
        </button>
      </form>
      <button
        type="button"
        className="link"
        onClick={() => {
          setMode(mode === 'login' ? 'register' : 'login');
          setError(null);
        }}
      >
        {mode === 'login' ? '¿No tienes cuenta? Regístrate' : '¿Ya tienes cuenta? Inicia sesión'}
      </button>
    </section>
  );
}
