// Live, on-input validation for Register and Profile pages
// Uses Bootstrap is-valid/is-invalid classes and writes messages into existing span[asp-validation-for]

(function () {
  const pagePath = window.location.pathname.toLowerCase();
  const isRegister = pagePath.includes('/account/register');
  const isProfile = pagePath.includes('/account/profile');
  if (!isRegister && !isProfile) return;

  // Helpers
  const $ = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

  const validators = {
    name(value) {
      const trimmed = value.trim();
      if (trimmed.length === 0) return { ok: false, msg: 'Full Name is required.' };
      if (trimmed.length < 3 || trimmed.length > 50) return { ok: false, msg: 'Full Name must be 3–50 characters.' };
      const re = /^[A-Za-z\s]+$/;
      if (!re.test(trimmed)) return { ok: false, msg: 'Only alphabets and spaces are allowed.' };
      return { ok: true };
    },
    emailFormat(value) {
      const trimmed = value.trim();
      if (!trimmed) return { ok: false, msg: 'Email is required.' };
      const re = /^[\w.-]+@[\w.-]+\.[A-Za-z0-9]{2,}$/;
      if (!re.test(trimmed)) return { ok: false, msg: 'Enter a valid email (example@domain.com).' };
      return { ok: true };
    },
    async emailUnique(value) {
      const url = isRegister ? '/Account/IsEmailAvailable' : '/Account/IsEmailAvailableForEdit';
      try {
        const r = await fetch(`${url}?email=${encodeURIComponent(value)}`, { credentials: 'same-origin' });
        const ok = await r.json();
        return ok ? { ok: true } : { ok: false, msg: 'This email is already registered.' };
      } catch {
        // On error, do not block typing
        return { ok: true };
      }
    },
    password(value) {
      const re = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/;
      if (!value) return { ok: false, msg: 'Password is required.' };
      if (!re.test(value)) return { ok: false, msg: 'Min 8 chars, with upper, lower, number, and special character.' };
      const common = new Set(['123456','password','admin','12345678','qwerty','letmein','welcome','iloveyou']);
      if (common.has(value.toLowerCase())) return { ok: false, msg: 'This password is too common/weak.' };
      return { ok: true };
    },
    confirmPassword(value, passwordVal) {
      if (!value) return { ok: false, msg: 'Confirm Password is required.' };
      if (value !== passwordVal) return { ok: false, msg: 'Passwords do not match.' };
      return { ok: true };
    },
    phone(value) {
      const trimmed = value.trim();
      if (!trimmed) return { ok: false, msg: 'Phone number is required.' };
      // Allow +91 optional, then 10 digits starting with 6-9
      const re = /^(?:\+?91[-\s]?)?[6-9]\d{9}$/;
      if (!re.test(trimmed)) return { ok: false, msg: 'Enter a valid 10-digit Indian phone (optional +91).' };
      return { ok: true };
    },
    async phoneUnique(value) {
      try {
        const r = await fetch(`/Account/IsPhoneAvailable?phone=${encodeURIComponent(value)}`, { credentials: 'same-origin' });
        const ok = await r.json();
        return ok ? { ok: true } : { ok: false, msg: 'This phone is already linked to another account.' };
      } catch {
        return { ok: true };
      }
    },
    address(value) {
      const trimmed = value.trim();
      if (!trimmed) return { ok: true }; // Address may be optional at register; required for profile completion by server
      const re = /^[A-Za-z0-9\s,\-.]+$/;
      if (!re.test(trimmed)) return { ok: false, msg: 'Use letters, numbers, comma, hyphen, dot only.' };
      if (trimmed.length < 10 || trimmed.length > 200) return { ok: false, msg: 'Address must be 10–200 characters.' };
      return { ok: true };
    }
  };

  // UI helpers
  function setFieldState(input, ok, msg) {
    const group = input.closest('.form-group, .mb-3');
    if (!group) return;
    const span = group.querySelector('span.field-validation-valid, span.text-danger,[data-valmsg-for]');
    input.classList.remove('is-valid', 'is-invalid');
    if (ok) {
      input.classList.add('is-valid');
      if (span) span.textContent = '';
    } else {
      input.classList.add('is-invalid');
      if (span) span.textContent = msg || 'Invalid value';
    }
  }

  // Debounce for async checks
  function debounce(fn, delay = 400) {
    let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); };
  }

  const form = $('form');
  if (!form) return;

  const submitBtn = $('button[type="submit"], input[type="submit"]', form);

  function collectValidity() {
    return $$('.is-invalid', form).length === 0;
  }
  function refreshSubmitState() {
    if (!submitBtn) return;
    submitBtn.disabled = !collectValidity();
  }

  // Wire fields
  const nameInput = $('input[name="Name"]', form);
  if (nameInput) {
    nameInput.addEventListener('input', () => {
      const res = validators.name(nameInput.value);
      setFieldState(nameInput, res.ok, res.msg);
      refreshSubmitState();
    });
  }

  const emailInput = $('input[name="Email"]', form);
  if (emailInput) {
    const checkEmailAsync = debounce(async () => {
      const fmt = validators.emailFormat(emailInput.value);
      if (!fmt.ok) { setFieldState(emailInput, false, fmt.msg); refreshSubmitState(); return; }
      const uniq = await validators.emailUnique(emailInput.value);
      setFieldState(emailInput, uniq.ok, uniq.msg);
      refreshSubmitState();
    });
    emailInput.addEventListener('input', checkEmailAsync);
  }

  const passwordInput = isRegister ? $('input[name="Password"]', form) : $('input[name="NewPassword"]', form);
  if (passwordInput) {
    passwordInput.addEventListener('input', () => {
      const res = validators.password(passwordInput.value);
      setFieldState(passwordInput, res.ok, res.msg);
      if (confirmInput) {
        const res2 = validators.confirmPassword(confirmInput.value, passwordInput.value);
        setFieldState(confirmInput, res2.ok, res2.msg);
      }
      refreshSubmitState();
    });
  }

  const confirmInput = isRegister ? $('input[name="ConfirmPassword"]', form) : $('input[name="ConfirmPassword"]', form);
  if (confirmInput) {
    confirmInput.addEventListener('input', () => {
      const baseVal = passwordInput ? passwordInput.value : '';
      const res = validators.confirmPassword(confirmInput.value, baseVal);
      setFieldState(confirmInput, res.ok, res.msg);
      refreshSubmitState();
    });
  }

  const phoneInput = $('input[name="PhoneNumber"]', form);
  if (phoneInput) {
    const checkPhoneAsync = debounce(async () => {
      const base = validators.phone(phoneInput.value);
      if (!base.ok) { setFieldState(phoneInput, false, base.msg); refreshSubmitState(); return; }
      const uniq = await validators.phoneUnique(phoneInput.value);
      setFieldState(phoneInput, uniq.ok, uniq.msg);
      refreshSubmitState();
    });
    phoneInput.addEventListener('input', checkPhoneAsync);
  }

  const addressInput = $('textarea[name="Address"], input[name="Address"]', form);
  if (addressInput) {
    addressInput.addEventListener('input', () => {
      const res = validators.address(addressInput.value);
      setFieldState(addressInput, res.ok, res.msg);
      refreshSubmitState();
    });
  }

  // Initial run to set state when form loads with values
  ['input', 'change'].forEach(evt => form.dispatchEvent(new Event(evt)));
})();