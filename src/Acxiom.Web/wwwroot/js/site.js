// auto-hide the toast
document.querySelectorAll('.toast.show').forEach(t => setTimeout(() => t.classList.remove('show'), 4000));

// filter dropdowns submit straight away
document.querySelectorAll('form.filters select').forEach(s => s.addEventListener('change', () => s.form.submit()));
